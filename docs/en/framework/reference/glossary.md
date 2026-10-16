# Glossary

**Read this in other languages:** [English](glossary.md) | [简体中文](../../../zh-CN/framework/reference/glossary.md)

Definitions of the terms used across Grow's documentation. Use these words consistently.

## View

One of the three structures an architecture is described with.

| View | Element | Describes |
| --- | --- | --- |
| Module | Module | Static structure: namespaces, assemblies and dependencies |
| Component-and-connector (C&C) | Component, connector | Runtime structure: services and the registry, events, and the loop |
| Allocation | — | Mapping of software units onto packages and the project |

## Layer

A level in the framework's dependency order. Each layer depends only on the ones below it:

`Core` → `Kernel` → `Capabilities` → `Framework` → `Game`.

The layer count is not the package count; packaging is the Allocation view.

## Core

The layer of passive primitives, such as logging. It owns no time and depends on nothing.

## Kernel

The layer of mechanisms: startup, lifetime, per-frame updates, module registration and scheduling.
It owns no game feature and never wraps one.

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

## Assembly

A compiled unit, described by an assembly definition (`asmdef`) in this project.

## Package

A distributable unit described by `package.json` and installed through the Unity Package Manager.

## Meta package

A package that assembles a recommended set of capabilities together with a default composition
root.
