using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OrderItemsReserverFunction.Models;

internal class OrderDetails
{
    public string id { get; set; }
    public Address ShippingAddress { get; set; }
    public IEnumerable<BasketItem> Items { get; set; }
    public decimal FinalPrice { get; set; }
}
