using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal static class NotFoundError
{
    public static AppError For(string label, int id) => AppError.NotFound($"{label} #{id} was not found.");
}
