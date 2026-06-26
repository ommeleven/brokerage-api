namespace std
{
    public class LivePriceFeed : IPriceFeed
    {
        public decimal GetPrice(string ticker)
        {
            return 195.34M;
        }
    }
}