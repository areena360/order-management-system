namespace OMS_Backend.Common.Exceptions
{
    public abstract class AppException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }
        public IDictionary<string, string[]>? Errors { get; }

        protected AppException(
            string message,
            int statusCode,
            string errorCode,
            IDictionary<string, string[]>? errors = null) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Errors = errors;
        }
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string entity, object key)
            : base($"{entity} with id '{key}' was not found.", StatusCodes.Status404NotFound, "NOT_FOUND") { }

        public NotFoundException(string message)
            : base(message, StatusCodes.Status404NotFound, "NOT_FOUND") { }
    }

    public class ValidationAppException : AppException
    {
        public ValidationAppException(IDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", StatusCodes.Status400BadRequest, "VALIDATION_ERROR", errors) { }

        public ValidationAppException(string message)
            : base(message, StatusCodes.Status400BadRequest, "VALIDATION_ERROR") { }
    }

    public class ConflictException : AppException
    {
        public ConflictException(string message)
            : base(message, StatusCodes.Status409Conflict, "CONFLICT") { }
    }

    public class UnauthorizedAppException : AppException
    {
        public UnauthorizedAppException(string message = "Unauthorized access.")
            : base(message, StatusCodes.Status401Unauthorized, "UNAUTHORIZED") { }
    }

    public class ForbiddenAppException : AppException
    {
        public ForbiddenAppException(string message = "You do not have permission to perform this action.")
            : base(message, StatusCodes.Status403Forbidden, "FORBIDDEN") { }
    }

    public class BadRequestAppException : AppException
    {
        public BadRequestAppException(string message)
            : base(message, StatusCodes.Status400BadRequest, "BAD_REQUEST") { }
    }
}