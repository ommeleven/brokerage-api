using System;

namespace std
{
    class Program
{
    static void Main(String[] args)
        {
            // var apple = new Stock("AAPL", 195.32M);
            // Console.WriteLine(apple.ValueFor(250));

            var Ticker = "AAPL";          // compiler infers string
            var price = 192.50m;          // compiler infers decimal
            var shares = 100;             // compiler infers int
            var apple = new Stock(Ticker, price);  // compiler infers Stock

            // These are IDENTICAL in behavior:
            var account1 = new Account("A1", "Alice");
            Account account2 = new Account("A2", "Bob");
            Console.WriteLine(apple.ValueFor(shares));
            Console.WriteLine(account1.Balance);

            // Code that depends on the INTERFACE, not the concrete class
            IPriceFeed feed = new StubPriceFeed();
            Console.WriteLine(feed.GetPrice("AAPL"));   // 100.00

            feed = new LivePriceFeed();                 // swap implementation, same variable type
            Console.WriteLine(feed.GetPrice("AAPL"));   // 192.50

            // var StockRepo = new Repository<Stock>();
            // StockRepo.Add(apple);
            // StockRepo.Add(apple);

            // var AccountRepo = new Repository<Account>();
            // AccountRepo.Add(account1);

            // Console.WriteLine(StockRepo.ToString());

            



        }

        

}
}