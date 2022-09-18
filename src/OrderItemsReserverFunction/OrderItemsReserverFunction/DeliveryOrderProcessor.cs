using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using Microsoft.Azure.Cosmos;
using OrderItemsReserverFunction.Models;

namespace DeliveryOrderProcessorFunction
{
    public static class DeliveryOrderProcessor
    {
        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function 'DeliveryOrderProcessor' processed a request.");

            var res = false;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                OrderDetails orderDetails = JsonConvert.DeserializeObject<OrderDetails>(requestBody);

                CosmosClient client = new CosmosClient(
                    "https://cosmosdb-apenkov.documents.azure.com:443/",
                    "f6K0mAlJEMiWnYNMvc59GORmitpVozKiW070GKj39LtQLWz5JSPJSCVg9TZGC62I7moiMVT5XDCevtdgUeIbmA==");

                var database = client.GetDatabase("eShopOnWeb");
                var orderDetailsContainer = database.GetContainer("OrderDetails");
                var itemResponse = await orderDetailsContainer.CreateItemAsync(orderDetails);
                res = itemResponse.StatusCode == HttpStatusCode.OK;
            }
            catch(Exception ex)
            {
                log.LogError(ex, "An exception has occurred");
            }

            return new OkObjectResult(res ? "Success" : "Failed");
        }
    }
}
