using System.Reflection.Metadata.Ecma335;

namespace std
{
    public class StubPriceFeed : IPriceFeed
    {
        public decimal GetPrice(string ticker) => 100M;
        
    }
}