# Introduction

PTrampert.SimplePatch is a flexible and lightweight .NET library designed to simplify the implementation of PATCH operations in RESTful APIs. It provides a robust framework for handling partial updates to resources, making it easier to work with JSON Patch and custom patching scenarios.

## Key Features

- **Strongly Typed Patch Objects:** Define patch models that map directly to your domain objects, ensuring type safety and clarity.
- **Optional Properties:** Use optional types to distinguish between omitted and explicitly set values, enabling precise control over updates.
- **Customizable Converters:** Extend or customize JSON serialization and deserialization for patch objects, supporting advanced scenarios.
- **Validation Support:** Integrate with data annotations and custom validation logic to ensure patch requests are valid before applying changes.

## Why Use PTrampert.SimplePatch?

Implementing PATCH endpoints can be challenging, especially when dealing with complex models and partial updates. PTrampert.SimplePatch abstracts away much of the boilerplate, allowing you to focus on your business logic while ensuring updates are handled safely and efficiently.

## Typical Use Cases

- Building RESTful APIs that support partial updates via PATCH requests.
- Handling scenarios where clients may send only a subset of fields to update.
- Enforcing validation and business rules on incoming patch data.
- Supporting both standard and custom patch formats.

## Getting Started

To learn how to integrate PTrampert.SimplePatch into your project, check out the [Getting Started](getting-started.md) guide.

---

For more details, see the API documentation and explore the sample projects included in the repository.