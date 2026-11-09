namespace AspireForge.Core.Configuration;

// How a solution is split into projects, which decides the direction dependencies may point in. Set as
// "architecture" in .aspireforge/config.json; when it isn't set, doctor detects it from project names.
public enum ArchitectureStyle
{
    // Domain <- Application <- Infrastructure, Api. What `aspireforge new` generates.
    Clean,

    // Presentation -> Business -> Data Access, all over a shared Entities project. Also called N-tier.
    Layered,
}
