using System;
using Microsoft.AspNetCore.Identity;

// Simple CLI to produce an ASP.NET Core Identity v3 password hash
// Usage: dotnet run --project Tools/IdentityPasswordHash -- <password>

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: dotnet run --project Tools/IdentityPasswordHash -- <password>");
            return 1;
        }

        var password = args[0];
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), password);

        Console.WriteLine(hash);
        return 0;
    }
}

