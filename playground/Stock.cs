using System;
using System.Linq.Expressions;

namespace std
{
    public class Stock
    {
        public string Ticker { get; set; }
        public decimal Price { get; set; } 

        public Stock(string ticker, decimal price) 
        {
            Ticker = ticker;
            Price = price;
        }

        public decimal ValueFor(int shares) => Price * shares;
    }
}