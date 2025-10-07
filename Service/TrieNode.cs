namespace StoreAndReturnListsOfAdvertisingPlatforms.Service;

internal class TrieNode
{
    public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Advertisers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public TrieNode? Parent { get; set; }

    // Получить дочерний узел, если существует, иначе null
    public TrieNode GetChild(string seg){
        Children.TryGetValue(seg, out var child);
        return child;
    }

    // Получить дочерний узел, если есть, иначе создать и вернуть его
    public TrieNode EnsureChild(string seg){
        if (!Children.TryGetValue(seg, out var child)){
            child = new TrieNode();
            Children[seg] = child;
        }
        return child;
    }

    // Добавить рекламодателей в HashSet для поиска
    public void CollectAdvertisers(HashSet<string> result){
        foreach (var adv in Advertisers) result.Add(adv);
    }
}