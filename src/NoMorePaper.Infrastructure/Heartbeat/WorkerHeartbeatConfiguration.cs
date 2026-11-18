using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NoMorePaper.Infrastructure.Heartbeat;

internal sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        // Names are spelled out because HeartbeatStore writes this table with plain SQL.
        builder.ToTable("worker_heartbeats");
        builder.HasKey(heartbeat => heartbeat.WorkerName);
        builder.Property(heartbeat => heartbeat.WorkerName).HasColumnName("worker_name").HasMaxLength(64);
        builder.Property(heartbeat => heartbeat.LastBeatAt).HasColumnName("last_beat_at");
    }
}
