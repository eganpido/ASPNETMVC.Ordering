using ASPNETMVC.Ordering.Data;
using ASPNETMVC.Ordering.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
        [HttpPost]
        public JsonResult CancelSale(int salesId)
        {
            try
            {
                var saleToDelete = db.TrnSales.FirstOrDefault(s => s.Id == salesId && s.IsLocked == false && s.IsTendered == false);

                if (saleToDelete == null)
                {
                    return Json(new { success = false, message = "Active order is locked." });
                }

                db.TrnSales.DeleteOnSubmit(saleToDelete);
                db.SubmitChanges();

                return Json(new { success = true, message = "Order cancelled successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ConfirmLockOrder(int salesId)
        {
            try
            {
                var sales = db.TrnSales.FirstOrDefault(s => s.Id == salesId);
                if (sales != null)
                {
                    sales.IsLocked = true;
                    db.SubmitChanges();

                    return Json(new { success = true, message = "Order locked successfully!" });
                }
                return Json(new { success = false, message = "Sales record not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UnlockOrder(int salesId)
        {
            try
            {
                var sales = db.TrnSales.FirstOrDefault(s => s.Id == salesId);
                if (sales != null)
                {
                    sales.IsLocked = false;
                    db.SubmitChanges();

                    return Json(new { success = true, message = "Order unlocked successfully!" });
                }
                return Json(new { success = false, message = "Sales record not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // SEPARATE METHODS PARA SA CART / ITEM OPERATIONS
        // ==========================================
        [HttpGet]
        public JsonResult GetCartItems(int salesId)
        {
            try
            {
                var lines = db.TrnSalesLines
                              .Where(l => l.SalesId == salesId)
                              .Select(l => new {
                                  lineId = l.Id,
                                  itemId = l.ItemId,
                                  name = l.MstItem.Alias, // Siguraduha nga mao ni ang property sa pangalan sa item sa imong MstItem table
                                  price = l.Price,
                                  qty = l.Quantity,
                                  preparation = l.Preparation
                              }).ToList();

                return Json(new { success = true, data = lines }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult AddItemToCart(int salesId, int itemId, decimal quantity, decimal price, string preparation)
        {
            try
            {
                var sales = db.TrnSales.FirstOrDefault(s => s.Id == salesId);
                if (sales == null)
                {
                    return Json(new { success = false, message = "Active sales record not found." });
                }

                var existingLine = db.TrnSalesLines.FirstOrDefault(l => l.SalesId == salesId && l.ItemId == itemId);

                if (existingLine != null)
                {
                    existingLine.Quantity += quantity;
                    existingLine.Amount = existingLine.Quantity * existingLine.Price;
                }
                else
                {
                    var item = db.MstItems.FirstOrDefault(i => i.Id == itemId);
                    var newLine = new TrnSalesLine
                    {
                        SalesId = salesId,
                        ItemId = itemId,
                        Quantity = quantity,
                        Amount = quantity * price,
                        IsPrinted = false,
                        UnitId = item.UnitId,
                        Cost = item.Cost,
                        Price = price,
                        DiscountId = 2,
                        DiscountRate = 0,
                        DiscountAmount = 0,
                        NetPrice = price,
                        TaxId = item.OutTaxId,
                        TaxRate = item.MstTax1.Rate,
                        TaxAmount = item.MstTax1.Rate > 0 ? (price / (1 + (item.MstTax1.Rate / 100))) * (item.MstTax1.Rate / 100) : 0m,
                        SalesAccountId = 159, 
                        AssetAccountId = 255, 
                        CostAccountId = 238, 
                        TaxAccountId = 87, 
                        SalesLineTimeStamp = DateTime.Now,
                        UserId = Convert.ToInt32(Session["UserId"]),
                        Preparation = preparation,
                        IsPrepared = false,
                        Price1 = 0,
                        Price2 = 0,
                        Price2LessTax = 0,
                        PriceSplitPercentage = 0,
                        BodegaItemQty = 0,
                        BodegaLocation = "",
                        ItemGroupId = null,
                        ItemTaxId = item.OutTaxId
                    };
                    db.TrnSalesLines.InsertOnSubmit(newLine);
                }
                db.SubmitChanges();

                UpdateSalesTotalAmount(salesId);

                return Json(new { success = true, message = "Item added successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult IncreaseQuantity(int lineId)
        {
            try
            {
                var salesLine = db.TrnSalesLines.FirstOrDefault(l => l.Id == lineId);
                if (salesLine != null)
                {
                    salesLine.Quantity += 1;
                    salesLine.Amount = salesLine.Quantity * salesLine.Price;
                    db.SubmitChanges();

                    UpdateSalesTotalAmount(salesLine.SalesId);

                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Item not found in cart." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DecreaseQuantity(int lineId)
        {
            try
            {
                var salesLine = db.TrnSalesLines.FirstOrDefault(l => l.Id == lineId);
                if (salesLine != null)
                {
                    int salesId = salesLine.SalesId;

                    if (salesLine.Quantity > 1)
                    {
                        salesLine.Quantity -= 1;
                        salesLine.Amount = salesLine.Quantity * salesLine.Price;
                    }
                    else
                    {
                        db.TrnSalesLines.DeleteOnSubmit(salesLine);
                    }
                    db.SubmitChanges();

                    UpdateSalesTotalAmount(salesId);

                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Item not found in cart." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult RemoveCartItem(int lineId)
        {
            try
            {
                var salesLine = db.TrnSalesLines.FirstOrDefault(l => l.Id == lineId);

                if (salesLine != null)
                {
                    int salesId = salesLine.SalesId;

                    db.TrnSalesLines.DeleteOnSubmit(salesLine);
                    db.SubmitChanges();

                    UpdateSalesTotalAmount(salesId);

                    return Json(new { success = true, message = "Item removed successfully!" });
                }

                return Json(new { success = false, message = "Item not found in cart." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult ClearCart(int salesId)
        {
            try
            {
                var salesLines = db.TrnSalesLines.Where(l => l.SalesId == salesId).ToList();

                if (salesLines.Any())
                {
                    db.TrnSalesLines.DeleteAllOnSubmit(salesLines);
                    db.SubmitChanges();
                }

                var sales = db.TrnSales.FirstOrDefault(s => s.Id == salesId);
                if (sales != null)
                {
                    sales.Amount = 0m;
                    sales.BalanceAmount = 0m;
                    db.SubmitChanges();
                }

                return Json(new { success = true, message = "Order items cleared successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult UpdatePreparation(int lineId, string preparation)
        {
            try
            {
                var salesLine = db.TrnSalesLines.FirstOrDefault(l => l.Id == lineId);
                if (salesLine != null)
                {
                    salesLine.Preparation = preparation;
                    db.SubmitChanges();

                    return Json(new { success = true, message = "Preparation updated successfully!" });
                }
                return Json(new { success = false, message = "Cart item not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        private void UpdateSalesTotalAmount(int salesId)
        {
            var sales = db.TrnSales.FirstOrDefault(s => s.Id == salesId);
            if (sales != null)
            {
                decimal totalAmount = db.TrnSalesLines
                                        .Where(l => l.SalesId == salesId)
                                        .Sum(l => (decimal?)l.Amount) ?? 0m;

                sales.Amount = totalAmount;
                sales.BalanceAmount = totalAmount; 

                db.SubmitChanges();
            }
        }
    }
}