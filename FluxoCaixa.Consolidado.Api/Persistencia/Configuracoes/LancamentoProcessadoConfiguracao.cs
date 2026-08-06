using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FluxoCaixa.Consolidado.Api.Persistencia.Configuracoes;

internal sealed class LancamentoProcessadoConfiguracao : IEntityTypeConfiguration<LancamentoProcessado>
{
    public void Configure(EntityTypeBuilder<LancamentoProcessado> builder)
    {
        builder.ToTable("lancamento_processado");

        builder.HasKey(lancamento => lancamento.LancamentoId);
        builder.Property(lancamento => lancamento.LancamentoId)
            .HasColumnName("lancamento_id")
            .ValueGeneratedNever();

        builder.Property(lancamento => lancamento.ComercianteId)
            .HasColumnName("comerciante_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(lancamento => lancamento.ProcessadoEm)
            .HasColumnName("processado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(lancamento => lancamento.ProcessadoEm)
            .HasDatabaseName("ix_lancamento_processado_processado_em");
    }
}
