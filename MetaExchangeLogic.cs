using MetaExchangeWPF;
using System;
using System.Collections.Generic;
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

        // Calculate the total available funds across all exchanges
        if (orderType == "Buy")
        {
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Crypto); // Assuming Crypto is the asset being bought
        }
        else // Sell
        {
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Crypto); // Assuming Crypto is being sold
        }

        // Check if the amount exceeds the total available funds
        exceedsLimit = amount > totalAvailableFunds;
        if (exceedsLimit)
        {
            return executionPlan; // No order can be fulfilled
        }

        // Proceed with the normal order execution logic if the amount is valid
        List<ExchangeOrder> relevantOrders;

        if (orderType == "Buy")
        {
            relevantOrders = exchanges.SelectMany(e => e.OrderBook.Asks.Select(a => new ExchangeOrder { Exchange = e.Id, Price = a.Order.Price, Amount = a.Order.Amount }))
                                      .OrderBy(o => o.Price)
                                      .ToList();
        }
        else // Sell
        {
            relevantOrders = exchanges.SelectMany(e => e.OrderBook.Bids.Select(b => new ExchangeOrder { Exchange = e.Id, Price = b.Order.Price, Amount = b.Order.Amount }))
                                      .OrderByDescending(o => o.Price)
                                      .ToList();
        }

        decimal totalAccumulatedAmount = 0m;

        // Track available funds for each exchange
        var availableFunds = exchanges.ToDictionary(e => e.Id, e => orderType == "Buy" ? e.AvailableFunds.Crypto : e.AvailableFunds.Crypto);

        foreach (var order in relevantOrders)
        {
            if (totalAccumulatedAmount >= amount)
                break;

            // Check if the exchange has enough available funds
            if (availableFunds[order.Exchange] <= 0)
                continue;

            decimal remainingAmount = amount - totalAccumulatedAmount;
            decimal orderAmountToTake = Math.Min(remainingAmount, Math.Min(order.Amount, availableFunds[order.Exchange])); // Ensure it doesn't exceed available funds

            // Reduce the available funds for that exchange
            availableFunds[order.Exchange] -= orderAmountToTake;

            // Add the executed order to the plan
            executionPlan.ExchangeOrders.Add(new ExchangeOrder
            {
                Exchange = order.Exchange,
                Price = order.Price,
                Amount = orderAmountToTake
            });

            totalAccumulatedAmount += orderAmountToTake;
        }

        if (executionPlan.ExchangeOrders.Any())
        {
            // Compute the weighted average price
            decimal totalCost = executionPlan.ExchangeOrders.Sum(o => o.Price * o.Amount);
            executionPlan.BestPrice = totalCost / totalAccumulatedAmount;
        }
        else
        {
            executionPlan.BestPrice = 0; // No suitable orders found
        }

        return executionPlan;
    }

}
