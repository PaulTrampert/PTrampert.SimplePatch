# PTrampert.Optionals

Library supporting "Optional" objects, distinct from nullable. Possibly useful for PATCH routes.

## Overview

**PTrampert.Optionals** is a C# library designed to facilitate the handling of flat PATCH objects in .NET web applications. With this library, you can easily distinguish between properties that are omitted from a PATCH request and those explicitly set to `null` or a value.

This is especially useful when you want to update only specific properties of an object, leaving others unchanged. For example, if you send a PATCH request like:

```json
{
  "name": "New Name"
}
```

and your object contains additional properties besides `name`, PTrampert.Optionals makes it easy to ensure that only `name` is updated, and all other properties remain untouched.

## Features

- Supports "Optional" types for PATCH operations.
- Distinguishes between omitted properties and properties set to `null`.
- Designed for use in .NET web APIs.
- Streamlines partial updates to complex objects.

## Getting Started

The library is intended for scenarios where you need precise control over which properties are updated during a PATCH request. If a property is omitted from the PATCH body, it will not be changed in the target object.

Code examples and installation instructions will be added soon.
