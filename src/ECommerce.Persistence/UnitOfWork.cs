using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Persistence.Contexts;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Persistence;

public class UnitOfWork(AppDbContext _context) : IUnitOfWork
{
    public async Task<int> Commit(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
