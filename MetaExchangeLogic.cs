using MetaExchangeWPF;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class MetaExchangeLogic
{
    public class ExecutionPlan
    {
        public decimal BestPrice { get; set; }
        public List<ExchangeOrder> ExchangeOrders { get; set; }
    }

    public class ExchangeOrder
    {
        public string Exchange { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
    }

    public static ExecutionPlan GetBestExecution(string orderType, decimal amount, List<ExchangeData> exchanges, out bool exceedsLimit, out decimal totalAvailableFunds)
    {
        var executionPlan = new ExecutionPlan
        {
            ExchangeOrders = new List<ExchangeOrder>()
        };

        List<ExchangeOrder> relevantOrders;

        if (orderType == "Buy")
        {
            // For Buy: Get all the ask orders (Crypto being sold)
            relevantOrders = exchanges.SelectMany(e => e.OrderBook.Asks.Select(a => new ExchangeOrder { Exchange = e.Id, Price = a.Order.Price, Amount = a.Order.Amount }))
                                      .OrderBy(o => o.Price)
                                      .ToList();

            // For Buy: Calculate total available Crypto across all exchanges
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Crypto);

            // Check if the amount exceeds the available Crypto
            exceedsLimit = amount > totalAvailableFunds;
        }
        else // Sell
        {
            // For Sell: Get all the bid orders (people buying Crypto)
            relevantOrders = exchanges.SelectMany(e => e.OrderBook.Bids.Select(b => new ExchangeOrder { Exchange = e.Id, Price = b.Order.Price, Amount = b.Order.Amount }))
                                      .OrderByDescending(o => o.Price)
                                      .ToList();

            // For Sell: First, calculate the potential Euro we'd get for selling our Crypto
            decimal totalEuroForCrypto = 0m;
            decimal totalAccumulatedAmount = 0m;

            foreach (var order in relevantOrders)
            {
                if (totalAccumulatedAmount >= amount)
                    break;

                decimal remainingAmount = amount - totalAccumulatedAmount;
                decimal orderAmountToTake = Math.Min(remainingAmount, order.Amount);

                // Calculate how much Euro we would get from this order
                totalEuroForCrypto += orderAmountToTake * order.Price;
                totalAccumulatedAmount += orderAmountToTake;
            }

            // Now that we have the total Euro we would get, let's compare it with the available Euro funds
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Euro);
            exceedsLimit = totalEuroForCrypto > totalAvailableFunds; // Assign value to exceedsLimit
        }

        if (exceedsLimit)
        {
            return executionPlan; // No order can be fulfilled
        }

        // Execute the order if the limits are not exceeded
        decimal finalAccumulatedAmount = 0m;
        var availableFunds = exchanges.ToDictionary(e => e.Id, e => orderType == "Buy" ? e.AvailableFunds.Crypto : e.AvailableFunds.Euro);

        foreach (var order in relevantOrders)
        {
            if (finalAccumulatedAmount >= amount)
                break;

            // Check if the exchange has enough available funds
            if (availableFunds[order.Exchange] <= 0)
                continue;

            decimal remainingAmount = amount - finalAccumulatedAmount;
            decimal orderAmountToTake = Math.Min(remainingAmount, Math.Min(order.Amount, availableFunds[order.Exchange]));

            // Initialize the exchange object before accessing it
            var exchange = exchanges.FirstOrDefault(e => e.Id == order.Exchange);
            if (exchange == null) continue;

            // For Sell: Check if the exchange has enough Euro to fulfill the sell order
            if (orderType == "Sell")
            {
                // Calculate the Euro needed for this order
                decimal euroRequired = orderAmountToTake * order.Price;

                // Check if the exchange has enough Euro
                if (exchange.AvailableFunds.Euro < euroRequired)
                {
                    // If not enough Euro is available, skip this order
                    continue;
                }

                // Reduce the Euro balance on the exchange
                exchange.AvailableFunds.Euro -= euroRequired;
            }

            // Reduce available funds on the exchange after each order execution
            availableFunds[order.Exchange] -= orderAmountToTake;

            // Update exchange data (Available funds, orders, etc.)
            if (orderType == "Buy")
            {
                exchange.AvailableFunds.Euro += orderAmountToTake * order.Price;
                exchange.AvailableFunds.Crypto -= orderAmountToTake;

                var askOrder = exchange.OrderBook.Asks.FirstOrDefault(a => a.Order.Price == order.Price && a.Order.Amount == order.Amount);
                if (askOrder != null)
                {
                    askOrder.Order.Amount -= orderAmountToTake;
                    if (askOrder.Order.Amount <= 0)
                        exchange.OrderBook.Asks.Remove(askOrder);
                }
            }
            else // Sell
            {
                exchange.AvailableFunds.Crypto += orderAmountToTake;

                var bidOrder = exchange.OrderBook.Bids.FirstOrDefault(b => b.Order.Price == order.Price && b.Order.Amount == order.Amount);
                if (bidOrder != null)
                {
                    bidOrder.Order.Amount -= orderAmountToTake;
                    if (bidOrder.Order.Amount <= 0)
                        exchange.OrderBook.Bids.Remove(bidOrder);
                }
            }

            // Write the updated exchange data back to the JSON file
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges", $"{exchange.Id}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(exchange, Formatting.Indented));

            // Add the executed order to the plan
            executionPlan.ExchangeOrders.Add(new ExchangeOrder
            {
                Exchange = order.Exchange,
                Price = order.Price,
                Amount = orderAmountToTake
            });

            finalAccumulatedAmount += orderAmountToTake;
        }

        if (executionPlan.ExchangeOrders.Any())
        {
            // Compute the weighted average price
            decimal totalCost = executionPlan.ExchangeOrders.Sum(o => o.Price * o.Amount);
            executionPlan.BestPrice = totalCost / finalAccumulatedAmount;
        }
        else
        {
            executionPlan.BestPrice = 0; // No suitable orders found
        }

        return executionPlan;
    }

}
