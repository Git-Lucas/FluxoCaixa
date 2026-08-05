namespace FluxoCaixa.Plataforma.LimiteDeTaxa;

/// <summary>Rajada e taxa por segundo de um limitador de tokens.</summary>
public readonly record struct LimitesDeTaxa(int Rajada, int TaxaPorSegundo);
