namespace std
{
    public class Repository<T> where T : IIdentifiable
    {
        private readonly Dictionary<string, T> _items = new();

        public void Add(T item) => _items[item.Id] = item;
        public T? GetById(string id) => _items.GetValueOrDefault(id);
        // public IEnumerable<T> All() => _items;

    }
}