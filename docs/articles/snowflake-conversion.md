# Snowflake Conversion

Discord IDs are 64-bit `ulong` snowflakes. Most relational providers — including
PostgreSQL/Npgsql and SQL Server — lack native unsigned 64-bit support and store
integers as signed `long`. Persistord bridges the gap in one place so you never
have to annotate individual properties.

## How it works

`Persistord.Core` ships two EF Core value converters:

- `UlongToLongConverter` — converts `ulong` ↔ `long` using `unchecked((long)value)`
  and `unchecked((ulong)value)`.
- `NullableUlongToLongConverter` — the same round-trip for `ulong?` ↔ `long?`.

The `unchecked` cast is bit-faithful: the 64 bits are reinterpreted without range
checking, so every value — including snowflakes with the high bit set — survives
storage exactly.

## Global registration

`DiscordDbContext` registers both converters in `ConfigureConventions`, so the
conversion applies to every `ulong` and `ulong?` property in your derived context
automatically:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
    builder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
}
```

You never annotate individual IDs. Inherit `DiscordDbContext` and all snowflake
properties in your model — including those in module entities — are handled.

## Storage note

Snowflakes are stored as `long` in the database column. Discord snowflakes remain
below `long.MaxValue` until roughly the year 2084, so signed storage is safe in
practice. The converter is nevertheless bit-faithful and would round-trip correctly
even past that point.

## See also

- [Core Graph](core-graph.md) — the base `DiscordDbContext` and the skeleton entities
  whose `ulong` IDs this conversion covers.
