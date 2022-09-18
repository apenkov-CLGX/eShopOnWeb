using System.ComponentModel.DataAnnotations;

namespace OrderItemsReserverFunction.Models;

public class BasketItem
{
    public string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
