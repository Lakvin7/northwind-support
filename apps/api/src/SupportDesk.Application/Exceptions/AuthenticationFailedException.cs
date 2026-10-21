namespace SupportDesk.Application.Exceptions;

public sealed class AuthenticationFailedException()
    : Exception("The email or password is incorrect.");
