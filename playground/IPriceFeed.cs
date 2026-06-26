namespace std
{
    public interface IPriceFeed
    {
        decimal GetPrice(string ticker)
        {
            return 12387.23M;
        }
    }
}