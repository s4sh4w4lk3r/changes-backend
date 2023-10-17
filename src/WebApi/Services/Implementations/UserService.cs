﻿using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities.Users;
using Models.Entities.Users.Auth;
using System.Net.Mail;
using WebApi.Services;
using WebApi.Services.Implementations;

namespace Services.Implementations;

public class UserService
{
    private readonly DbContext _dbContext;
    private readonly IValidator<User> _userValidator;
    private readonly ApprovalService _approvalService;
    private readonly DbSet<User> _users;

    public UserService(DbContext dbContext, IValidator<User> validator, ApprovalService approvalService)
    {
        _dbContext = dbContext;
        _userValidator = validator;
        _approvalService = approvalService;
        _users = _dbContext.Set<User>();
    }

    public async Task<ServiceResult> RegisterAsync(User user, CancellationToken cancellationToken = default)
    {
        var userVal = _userValidator.Validate(user, o => o.IncludeRuleSets("default", "password_regex_matching"));
        if (userVal.IsValid is false)
        {
            return new ServiceResult(false, userVal.ToString());
        }

        if ((await _users.AnyAsync(x => x.Email == user.Email && x.IsEmailConfirmed == true, cancellationToken)) is true)
        {
            return new ServiceResult(false, "Пользователь с таким Email уже есть в бд.");
        }

        if ((await _users.AnyAsync(x => x.Email == user.Email && x.IsEmailConfirmed == false, cancellationToken)) is true)
        {
            return new ServiceResult(false, "Пользователь с таким Email уже есть в бд, но Email не подтвержден.");
        }

        user.IsEmailConfirmed = false;
        user.Password = HashPassword(user.Password!);
        await _users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult(true, "Пользователь добавлен в базу, но Email не подтвержден.");
    }

    public async Task<ServiceResult> ConfirmEmailAsync(string userEmail, int approvalCode, CancellationToken cancellationToken = default)
    {
        if (approvalCode == default)
        {
            return new ServiceResult(false, "Некорректный approvalCode пользователя.");
        }

        var emailOk = MailAddress.TryCreate(userEmail, out _);
        if (emailOk is false)
        {
            return new ServiceResult(false, "Email имеет неправильный формат.");
        }

        var validUser = await _users.Where(e => e.Email == userEmail).FirstOrDefaultAsync(cancellationToken);
        if (validUser is null)
        {
            return new ServiceResult(false, "Пользователь для валидации не был найден в бд.");
        }

        var approvalServiceResult = await _approvalService.VerifyCodeAsync(validUser.UserId, approvalCode, ApprovalCode.ApprovalCodeType.Registration, cancellationToken);
        if (approvalServiceResult.Success is false)
        {
            return new ServiceResult(false, "Код подтверждения регистрации не принят.", approvalServiceResult);
        }

        validUser.IsEmailConfirmed = true;
        _dbContext.Set<User>().Update(validUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult(true, "Email пользователя подтвержден.");
    }

    public async Task<ServiceResult> UnregisterAsync(User user, int approvalCode, CancellationToken cancellationToken = default)
    {
#warning проверить работу

        var validUser = await _users.FirstOrDefaultAsync(e => e.UserId == user.UserId, cancellationToken);
        if (validUser is null)
        {
            return new ServiceResult(false, "Пользователь не найден в бд.");
        }

        var approvalServiceResult = await _approvalService.VerifyCodeAsync(validUser.UserId, approvalCode, ApprovalCode.ApprovalCodeType.Unregistration, cancellationToken);
        if (approvalServiceResult.Success is false)
        {
            return new ServiceResult(false, "Код подтверждения для удаления аккаунта не принят.", approvalServiceResult);
        }

        _users.Remove(validUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult(true, "Аккаунт пользователя удален.");
    }
    
    public async Task<ServiceResult> UpdateEmail(int newUserId, int approvalCode, CancellationToken cancellationToken = default)
    {
#warning не проверен
        if (newUserId == default)
        {
            return new ServiceResult(false, "Некорректный id пользователя.");
        }
        if (approvalCode == default)
        {
            return new ServiceResult(false, "Некорректный approvalCode пользователя.");
        }

        var validUser = await _users.FirstOrDefaultAsync(e => e.UserId == newUserId, cancellationToken);
        if (validUser is null)
        {
            return new ServiceResult(false, "Пользователь не найден в бд.");
        }

        var approvalServiceResult = await _approvalService.VerifyCodeAsync(validUser.UserId, approvalCode, ApprovalCode.ApprovalCodeType.UpdateMail, cancellationToken);
        if (approvalServiceResult.Success is false)
        {
            return new ServiceResult(false, "Код подтверждения для изменения почты не принят.", approvalServiceResult);
        }

        var validUserValResult = _userValidator.Validate(validUser, o => o.IncludeRuleSets("default"));
        if (validUserValResult.IsValid is false)
        {
            throw new Exception($"Пользователь из бд оказался невалидным\n{validUserValResult}");
        }

        validUser.Email = validUser.Email;
        _dbContext.Set<User>().Update(validUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult(true, "Email пользователя обновлен.");
    }

    public async Task<ServiceResult> UpdatePassword([Bind("UserId", "Password")]User newUser, int approvalCode, CancellationToken cancellationToken = default)
    {
#warning не проверен
        if (newUser.UserId == default)
        {
            return new ServiceResult(false, "Некорректный id пользователя.");
        }
        if (approvalCode == default)
        {
            return new ServiceResult(false, "Некорректный approvalCode пользователя.");
        }

        var valResult = _userValidator.Validate(newUser, o => o.IncludeRuleSets("password_regex"));
        if (valResult.IsValid is false)
        {
            return new ServiceResult(false, valResult.ToString());
        }

        var approvalServiceResult = await _approvalService.VerifyCodeAsync(newUser.UserId, approvalCode, ApprovalCode.ApprovalCodeType.UpdatePassword, cancellationToken);
        if (approvalServiceResult.Success is false)
        {
            return new ServiceResult(false, "Код подтверждения для изменения пароля не принят.", approvalServiceResult);
        }

        var validUser = await _users.FirstOrDefaultAsync(e => e.UserId == newUser.UserId, cancellationToken);
        if (validUser is null)
        {
            return new ServiceResult(false, "Пользователь с таким Id не найден.");
        }
        var validUserValResult = _userValidator.Validate(validUser, o => o.IncludeRuleSets("default"));
        if (validUserValResult.IsValid is false)
        {
            throw new Exception($"Пользователь из бд оказался невалидным\n{validUserValResult}");
        }

        validUser.Password = HashPassword(newUser.Password!);
        _users.Update(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult(true, "Пароль пользователя обновлен.");
    }

    public async Task<ServiceResult<User>> CheckLoginDataAsync(User user, CancellationToken cancellationToken = default)
    {
        var valResult = _userValidator.Validate(user, o => o.IncludeRuleSets("default", "password_regex_matching"));
        if (valResult.IsValid is false)
        {
            return new ServiceResult<User>(false, valResult.ToString(), null);
        }

        var userFromRepo = await _users.FirstOrDefaultAsync(e => e.Email == user.Email, cancellationToken);
        if (userFromRepo is null)
        {
            return new ServiceResult<User>(false, "Пользователя нет в бд.", null);
        }

        if (userFromRepo.IsEmailConfirmed is false)
        {
            return new ServiceResult<User>(false, "Пользователь найден, но Email не подтвержден.", null);
        }

        string? hashFromRepo = userFromRepo.Password;
        if (string.IsNullOrWhiteSpace(hashFromRepo))
        {
            return new ServiceResult<User>(false, "Пользователь найден, но его пароль почему-то пуст.", null);
        }

        if (ValidatePassword(hashFromRepo, user.Password!) is true)
        {
            return new ServiceResult<User>(true, "Пароль подтвержден.", userFromRepo);
        }
        else
        {
            return new ServiceResult<User>(false, "Пароль неверный.", null);
        }
    }

    private static string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password);

    private static bool ValidatePassword(string hash, string password) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}
