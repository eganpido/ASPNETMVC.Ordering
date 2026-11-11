using ASPNETMVC.Ordering.Data;
using ASPNETMVC.Ordering.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ASPNETMVC.Ordering.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderingDataContext db = new OrderingDataContext(ConfigurationManager.ConnectionStrings["liteclerk_orderingConnectionString"].ConnectionString);

        public ActionResult Index()
        {
            var tableGroups = db.MstTableGroups.ToList();
            var tables = db.MstTables.ToList();

            var tableListWithStatus = new List<TableStatusModel>();
            int terminalId = Convert.ToInt32(Session["TerminalId"]);
            foreach (var table in tables)
            {
                var sale = db.TrnSales
                             .Where(s => s.TableId == table.Id && s.IsTendered == false && s.TerminalId == terminalId && s.SalesDate == DateTime.Today)
                             .OrderByDescending(s => s.Id)
                             .FirstOrDefault();

                bool isOccupied = false;
                int activeSalesId = 0;

                if (sale != null)
                {
                    isOccupied = true;
                    activeSalesId = sale.Id;
                }

                tableListWithStatus.Add(new TableStatusModel
                {
                    Id = table.Id,
                    TableCode = table.TableCode,
                    TableGroupId = table.TableGroupId,
                    IsOccupied = isOccupied,
                    SalesId = activeSalesId 
                });
            }

            ViewBag.TableGroups = tableGroups;
            ViewBag.Tables = tableListWithStatus;

            return View();
        }

        public ActionResult OrderDetail(int tableId, int salesId)
        {
            var table = db.MstTables.FirstOrDefault(t => t.Id == tableId);
            if (table == null)
            {
                return HttpNotFound();
            }

            var activeSale = db.TrnSales.FirstOrDefault(s => s.Id == salesId && s.TableId == tableId);
            if (activeSale == null)
            {
                return HttpNotFound(); 
            }

            ViewBag.Table = table;
            ViewBag.ActiveSale = activeSale;
            ViewBag.SalesId = activeSale.Id;
            ViewBag.ItemGroups = db.MstItemGroups.ToList();
            ViewBag.ItemGroupItems = db.MstItemGroupItems.ToList();
            ViewBag.Items = db.MstItems.ToList();

            return View();
        }
        public String FillLeadingZeroes(Int32 number, Int32 length)
        {
            var result = number.ToString();
            var pad = length - result.Length;
            while (pad > 0)
            {
                result = '0' + result;
                pad--;
            }

            return result;
        }

        [HttpPost]
        public JsonResult CreateNewSale(int tableId)
        {
            try
            {
                var existingSale = db.TrnSales
                                     .Where(s => s.TableId == tableId && s.IsLocked == true && s.IsTendered == false && s.SalesDate == DateTime.Today)
                                     .OrderByDescending(s => s.Id)
                                     .FirstOrDefault();

                if (existingSale != null)
                {
                    return Json(new { success = true, salesId = existingSale.Id });
                }

                int currentUserId = Session["UserId"] != null ? Convert.ToInt32(Session["UserId"]) : 1;
                int terminalId = Session["TerminalId"] != null ? Convert.ToInt32(Session["TerminalId"]) : 1;
                var terminal = db.MstTerminals.Where(s => s.Id == terminalId).FirstOrDefault();
                var period = db.MstPeriods.FirstOrDefault();
                var customer= db.MstPeriods.FirstOrDefault();

                string salesNumber = "0000000001";

                while (true)
                {
                    var lastSales = db.TrnSales.OrderByDescending(d => d.Id).FirstOrDefault();

                    if (lastSales != null && int.TryParse(lastSales.SalesNumber, out int lastNumber))
                    {
                        int newSalesNumber = lastNumber + 1;
                        salesNumber = FillLeadingZeroes(newSalesNumber, 10);
                    }
                    else if (lastSales != null)
                    {
                        int newSalesNumber = lastSales.Id + 1;
                        salesNumber = FillLeadingZeroes(newSalesNumber, 10);
                    }

                    var existingSales = db.TrnSales.Any(d => d.SalesNumber == salesNumber);

                    if (!existingSales)
                    {
                        break;
                    }
                }

                var newSale = new TrnSale
                {
                    PeriodId = period.Id,
                    SalesDate = DateTime.Today,
                    SalesNumber = salesNumber,
                    ManualInvoiceNumber = terminal.Terminal + "-" + salesNumber,
                    CollectionNumber = null,
                    Amount = 0,
                    TableId = tableId,
                    TableStatus = 0,
                    CustomerId = 5579,
                    AccountId = 64,
                    TermId = 7,
                    DiscountId = null,
                    SeniorCitizenId = "",
                    SeniorCitizenName = "",
                    SeniorCitizenAge = null,
                    Remarks = "",
                    SalesAgent = currentUserId,
                    TerminalId = terminalId,
                    DeliveryId = null,
                    DeliveryDriver = "",
                    PreparedBy = currentUserId,
                    CheckedBy = currentUserId,
                    ApprovedBy = currentUserId,
                    IsLocked = false,
                    IsTendered = false,
                    IsCancelled = false,
                    IsDispatched = false,
                    IsReturned = false,
                    ReturnApplication = "",
                    PaidAmount = 0,
                    CreditAmount = 0,
                    DebitAmount = 0,
                    BalanceAmount = 0,
                    EntryUserId = currentUserId,
                    EntryDateTime = DateTime.Now,
                    UpdateUserId = currentUserId,
                    UpdateDateTime = DateTime.Now,
                    Pax = null,
                    PostCode = null,
                    IsDelivery = false,
                    DeliveryType = "Paid",
                    //NAACId = null,
                    //NAACName = "",
                    //SoloParentId = null,
                    //SoloParentName = "",
                    //ChildName = "",
                    //ChildBirthday = null,
                    //ChildAge = null,
                    //PWDSeniorTIN = "",
                    //IsManualSalesInvoice = false
                };

                db.TrnSales.InsertOnSubmit(newSale);
                db.SubmitChanges(); 
                return Json(new { success = true, salesId = newSale.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // SEPARATE METHODS PARA SA CART / ITEM OPERATIONS
        // ==========================================

        [HttpPost]
        public JsonResult AddItemToCart(int salesId, int itemId, decimal quantity, decimal price)
        {
            try
            {
                // Diri nimo isulod ang logic sa pag-insert sa TrnSalesLine gamit ang salesId
                // Pananglitan:
                /*
                var newLine = new TrnSalesLine
                {
                    SalesId = salesId,
                    ItemId = itemId,
                    Quantity = quantity,
                    Price = price,
                    Amount = quantity * price
                };
                db.TrnSalesLines.InsertOnSubmit(newLine);
                db.SubmitChanges();
                */

                return Json(new { success = true, message = "Item added successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UpdateCartItem(int lineId, decimal quantity)
        {
            try
            {
                // Logic sa pag-update sa quantity sa TrnSalesLine
                return Json(new { success = true, message = "Cart updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteCartItem(int lineId)
        {
            try
            {
                // Logic sa pagtangtang sa item gikan sa TrnSalesLine
                return Json(new { success = true, message = "Item removed successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}