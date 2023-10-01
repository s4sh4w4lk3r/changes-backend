﻿using System.Text.RegularExpressions;

namespace Models.Entities.Users;

public abstract class User
{
    public int UserPK { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }

    protected User(int userPK, string? email, string? password)
    {
        email.ThrowIfNull().IfWhiteSpace().IfNotMatches(new Regex("^\\S+@\\S+\\.\\S+$"));
        UserPK = userPK;
        Email = email;
        Password = password;
    }
}