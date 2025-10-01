using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebUI.Infrastructure.Repositories
{
    public interface IRepository
    {
        Task<T?> GetByIdAsync<T>(int id) where T : class;
        Task<List<T>> ListAsync<T>() where T : class;
        Task AddAsync<T>(T entity) where T : class;
        void Update<T>(T entity) where T : class;
        void Delete<T>(T entity) where T : class;
        Task<int> SaveChangesAsync();
    }
}
