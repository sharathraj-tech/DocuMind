using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public sealed record Result<T>
    {
        public T? Value { get; private init; }
        public string? Error { get; private init; }
        public int Status { get; private init; } = 200;
        public bool Succeeded => Error is null;

        public static Result<T> Ok(T value) => new() { Value = value };
        public static Result<T> Fail(string error, int status) => new() { Error = error, Status = status };
    }
}
