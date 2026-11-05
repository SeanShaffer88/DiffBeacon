# ADR: Four reusable modules

Status: proposed extraction implemented for alpha evaluation.

Extract Text, Files, Formats and Sessions while retaining namespace DiffBeacon.Core and a Core type-forwarding facade. Move existing implementations without algorithm rewrites. Files depends on Text for shared status/document contracts; Sessions depends on Files and Text; Formats and Text use the framework alone.

This permits selective consumption without an assembly for every helper. Retained namespaces preserve source usage; forwarding preserves ordinary managed type resolution. Reflection assumptions about defining assemblies change. See MODULES.md for entry points, limits, effects and executable examples.
