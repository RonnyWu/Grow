# Glossary

**Read this in other languages:** [English](glossary.md) | [简体中文](../../../zh-CN/framework/reference/glossary.md)

The meaning of each term used across Grow's documentation.

## View

One of the three structures an architecture is described with.

| View | Element | Describes |
| --- | --- | --- |
| Module | Module | Static structure: namespaces, assemblies and dependencies |
| Component-and-connector (C&C) | Component, connector | Runtime structure: services and the registry, events, and the loop |
| Allocation | — | Mapping of software units onto packages and the project |

## Layer

A level in the framework's dependency order. Each layer depends only on the ones below it:

| Layer | What it is |
| --- | --- |
| `Core` | Passive primitives, such as logging. It owns no time and depends on nothing. |
| `Kernel` | Mechanisms: startup, lifetime, per-frame updates, module registration and scheduling. It owns no game feature and never wraps one. |
| `Capabilities` | Game features, each shipped as its own package and exposed through a contract. |
| `Framework` | A meta package that assembles a recommended set of capabilities. |
| `Game` | The game itself — the application code that consumes the framework. |

The layer count is not the package count; packaging is the Allocation view, and one package can span
layers.

`Framework` also names the whole thing these pages describe. In a list of layers it means the meta
package.

## Mechanism

Something the framework does to make a game run. Mechanisms belong to the kernel and are not
abstracted.

## Capability

A game feature such as assets, saves, UI or audio. A capability ships as its own package and is
not owned by the kernel.

## Contract

The public API a capability exposes. It never contains a third-party type, so the implementation
behind it can be replaced.

## Implementation

The code behind a contract, usually wrapping a third-party library.

## Module

A code unit with a compile-time boundary: a namespace plus an assembly definition.

## Component

A runtime unit, such as a module instance or a service.

## Connector

An interaction between components — a direct call, an event, or a scheduled callback.

## Assembly

A compiled unit, described by an assembly definition (`asmdef`) in this project.

## Package

A distributable unit described by `package.json` and installed through the Unity Package Manager.

## Composition root

The single place where a game's implementations are assembled and wired to the contracts they
implement. A meta package ships a default one.

## Meta package

A package that assembles a recommended set of capabilities together with a default composition
root.
