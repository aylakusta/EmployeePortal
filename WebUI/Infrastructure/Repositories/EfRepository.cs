using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;

namespace WebUI.Infrastructure.Repositories
{
    public class EfRepository : IRepository
    {
        private readonly ApplicationDbContext _db;
        public EfRepository(ApplicationDbContext db) => _db = db;

        public async Task<T?> GetByIdAsync<T>(int id) where T : class
            => await _db.Set<T>().FindAsync(id);

        public async Task<List<T>> ListAsync<T>() where T : class
            => await _db.Set<T>().ToListAsync();

        public async Task AddAsync<T>(T entity) where T : class
            => await _db.Set<T>().AddAsync(entity);

        public void Update<T>(T entity) where T : class
            => _db.Set<T>().Update(entity);

        public void Delete<T>(T entity) where T : class
            => _db.Set<T>().Remove(entity);

        public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
    }
}
