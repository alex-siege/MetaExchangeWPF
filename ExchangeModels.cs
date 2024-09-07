namespace MetaExchangeWPF
{
    public class ExchangeData
    {
        public string Id { get; set; }
        public AvailableFunds AvailableFunds { get; set; }
        public OrderBook OrderBook { get; set; }
    }

    public class AvailableFunds
    {
        public decimal Crypto { get; set; }
        public decimal Euro { get; set; }
    }

    public class OrderBook
    {
        public List<BidAsk> Bids { get; set; }
        public List<BidAsk> Asks { get; set; }
    }

    public class BidAsk
    {
        public Order Order { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
    }

    public class ExchangeOrder
    {
        public string Exchange { get; set; }
        public Order Order { get; set; }
    }

}

