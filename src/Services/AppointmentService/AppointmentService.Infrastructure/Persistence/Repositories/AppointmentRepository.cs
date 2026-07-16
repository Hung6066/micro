using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.AppointmentService.Infrastructure.Persistence.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppointmentDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public AppointmentRepository(AppointmentDbContext context) =>
        _context = context;

    public async Task<Appointment?> GetByIdAsync(AppointmentId id, CancellationToken ct = default) =>
        await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default) =>
        await _context.Appointments
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetByPatientIdAsync(
        Guid patientId, int page, int pageSize, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = _context.Appointments.Where(a => a.PatientId == patientId);

        if (fromDate.HasValue)
            query = query.Where(a => a.ScheduledDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.ScheduledDate <= toDate.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.ScheduledDate)
            .ThenBy(a => a.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Appointment>> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default) =>
        await _context.Appointments
            .Where(a => a.ProviderId == providerId)
            .OrderByDescending(a => a.ScheduledDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Appointment> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Appointments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(a =>
                (a.Reason != null && EF.Functions.ILike(a.Reason, pattern)) ||
                (a.Notes != null && EF.Functions.ILike(a.Notes, pattern)) ||
                EF.Functions.ILike(a.Status.Code, pattern) ||
                EF.Functions.ILike(a.Type.Code, pattern));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.ScheduledDate)
            .ThenBy(a => a.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default)
    {
        await _context.Appointments.AddAsync(appointment, ct);
        return appointment;
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
        _context.Entry(appointment).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(AppointmentId id, CancellationToken ct = default) =>
        await _context.Appointments.AnyAsync(a => a.Id == id, ct);
}
