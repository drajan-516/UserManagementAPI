using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public enum UserOpStatus { Success, NotFound, EmailConflict }
public readonly record struct UserOpResult(UserOpStatus Status, User? User = null);

public interface IUserService
{
    IReadOnlyList<User> GetAll(string? department, int page, int pageSize);
    User? GetById(int id);
    UserOpResult Create(UserRequest request);
    UserOpResult Update(int id, UserRequest request);
    bool Delete(int id);
}

public class UserService : IUserService
{
    private readonly object _lock = new();
    private readonly SortedDictionary<int, User> _users = new();                 // уже отсортировано по Id
    private readonly Dictionary<string, int> _emailIndex =
        new(StringComparer.OrdinalIgnoreCase);                                    // поиск email за O(1)
    private int _nextId;

    public IReadOnlyList<User> GetAll(string? department, int page, int pageSize)
    {
        lock (_lock)
        {
            IEnumerable<User> q = _users.Values;
            if (!string.IsNullOrWhiteSpace(department))
                q = q.Where(u => u.Department.Equals(department, StringComparison.OrdinalIgnoreCase));

            // ToList внутри lock, чтобы коллекция не менялась во время перечисления
            return q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
    }

    public User? GetById(int id)
    {
        lock (_lock) return _users.TryGetValue(id, out var u) ? u : null;
    }

    public UserOpResult Create(UserRequest r)
    {
        lock (_lock)
        {
            if (_emailIndex.ContainsKey(r.Email))
                return new(UserOpStatus.EmailConflict);

            var user = new User
            {
                Id = ++_nextId,
                FirstName = r.FirstName, LastName = r.LastName,
                Email = r.Email, Department = r.Department, Role = r.Role,
                CreatedAt = DateTime.UtcNow
            };
            _users[user.Id] = user;
            _emailIndex[user.Email] = user.Id;
            return new(UserOpStatus.Success, user);
        }
    }

    public UserOpResult Update(int id, UserRequest r)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var existing))
                return new(UserOpStatus.NotFound);

            if (_emailIndex.TryGetValue(r.Email, out var ownerId) && ownerId != id)
                return new(UserOpStatus.EmailConflict);

            // Новый неизменяемый объект вместо правки существующего
            var updated = existing with
            {
                FirstName = r.FirstName, LastName = r.LastName,
                Email = r.Email, Department = r.Department, Role = r.Role,
                UpdatedAt = DateTime.UtcNow
            };

            _emailIndex.Remove(existing.Email);
            _emailIndex[updated.Email] = id;
            _users[id] = updated;
            return new(UserOpStatus.Success, updated);
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            if (!_users.Remove(id, out var removed)) return false;
            _emailIndex.Remove(removed.Email);
            return true;
        }
    }
}