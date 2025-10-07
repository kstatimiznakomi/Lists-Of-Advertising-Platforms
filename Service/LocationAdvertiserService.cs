namespace StoreAndReturnListsOfAdvertisingPlatforms.Service{
    public sealed class LocationAdvertiserService{
        // корень текущего дерева; заменяется атомарно в LoadFromText
        private TrieNode _root = new();
        private readonly ReaderWriterLockSlim _lock = new();

        /// <summary>
        /// Нормализует строку локации.
        /// Возвращает null для null/whitespace (как ожидают тесты).
        /// Нормализованный вид: "/" для корня или "/segment1/segment2" (leading slash).
        /// </summary>
        public string? NormalizeLocation(string? input){
            if (string.IsNullOrWhiteSpace(input)) return null;

            var s = input.Trim().Replace('\\', '/');

            // если единственный символ слэш -> root
            if (s == "/" || string.IsNullOrEmpty(s)) return "/";


            // нормализуем множественные слэши внутри и всё в нижний регистр
            var parts = s.Split(new[]{ '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Select(p => string.Concat(p.Where(c => !char.IsWhiteSpace(c))))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
            return parts.Length == 0 ? "/" : $"/{string.Join('/', parts).ToLowerInvariant()}";
        }

        /// <summary>
        /// Атомарно заменяет текущее дерево рекламодателей содержимым из текста.
        /// Формат строки: "Name: loc1, loc2, ..." (локации могут иметь или не иметь ведущего слэша).
        /// Игнорируются строки без "Name:" или с пустым именем.
        /// </summary>
        public void LoadFromText(string? text){
            // подготовим новый корень локально (чтобы замена была атомарной)
            var newRoot = new TrieNode();

            if (!string.IsNullOrWhiteSpace(text)){
                var lines = text.Split(new[]{ '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var raw in lines){
                    var line = raw.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // ищем первое двоеточие
                    var idx = line.IndexOf(':');
                    if (idx <= 0) continue; // либо нет двоеточия, либо имя пустое

                    var name = line[..idx].Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var locPart = line[(idx + 1)..].Trim();
                    // если после ':' пусто — считаем это некорректной строкой
                    if (string.IsNullOrWhiteSpace(locPart)) continue;

                    // разделение локаций через запятую
                    var locs = locPart.Split(new[]{ ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrEmpty(l));

                    foreach (var rawLoc in locs){
                        var normalized = NormalizeLocation(rawLoc);
                        if (normalized == null) continue; // игнорируем пустые/нормализованные в null
                        InsertIntoTrie(newRoot, normalized, name);
                    }
                }
            }

            // атомарно заменяем корень под write-lock
            _lock.EnterWriteLock();
            try{
                _root = newRoot;
            }
            finally{
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Ищет рекламодателей по локации.
        /// - Возвращает пустую коллекцию для null/empty/whitespace входа.
        /// - Если запрос на корень ("/"), возвращает только root-рекламодателей.
        /// - Иначе возвращает рекламодателей для узла и всех его предков (включая root).
        /// </summary>
        public IEnumerable<string> Search(string? location){
            // вход null/whitespace -> пустая коллекция (тесты ожидают Empty)
            if (string.IsNullOrWhiteSpace(location)) return Enumerable.Empty<string>();

            var normalized = NormalizeLocation(location);
            if (normalized == null) return Enumerable.Empty<string>();

            _lock.EnterReadLock();
            try{
                // root case: только _root.Advertisers
                if (IsRoot(normalized)){
                    return _root.Advertisers.ToArray();
                }

                var segments = LocationToSegments(normalized).ToArray();
                if (segments.Length == 0) return _root.Advertisers.ToArray();

                var node = _root;
                foreach (var seg in segments){
                    if (!node.Children.TryGetValue(seg, out var next))
                        return Enumerable.Empty<string>(); // путь отсутствует
                    node = next;
                }

                // собираем рекламодателей с текущего узла вверх по Parent, включая root
                var collected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cur = node;
                while (cur != null){
                    foreach (var adv in cur.Advertisers){
                        collected.Add(adv);
                    }

                    cur = cur.Parent;
                }

                return collected.ToArray();
            }
            finally{
                _lock.ExitReadLock();
            }
        }

        // --- Вспомогательные реализации ---

        private static bool IsRoot(string normalized) => normalized == "/";

        private static IEnumerable<string> LocationToSegments(string normalized){
            return IsRoot(normalized)
                ? Enumerable.Empty<string>()
                : normalized.Trim('/').Split(new[]{ '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Вставка в предоставленный корень (используется при построении нового дерева)
        private static void InsertIntoTrie(TrieNode root, string normalizedLocation, string advertiser){
            if (IsRoot(normalizedLocation)){
                root.Advertisers.Add(advertiser);
                return;
            }

            var segments = LocationToSegments(normalizedLocation).ToArray();
            var node = root;
            foreach (var seg in segments){
                if (!node.Children.TryGetValue(seg, out var next)){
                    next = new TrieNode{ Parent = node };
                    node.Children[seg] = next;
                }

                node = next;
            }

            node.Advertisers.Add(advertiser);
        }
    }
}