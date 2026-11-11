namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Writes a line of the activity feed (app.ActivityLog) in the same unit of work as the change it describes: the entry
/// is saved by the same <see cref="IUnitOfWork.SaveChangesAsync"/> call as the change, so there is never an entry for a
/// change that failed, nor a change without its entry. The actor is the signed-in user (or the explicit system
/// identity) and the time comes from <see cref="IClock"/>.
/// Messages carry ids and codes only: never personal data, national ids, bank details or salaries.
/// </summary>
public interface IActivityRecorder
{
    /// <param name="category">One of <see cref="Domain.Activity.ActivityCategories"/>.</param>
    /// <param name="message">The wording (see <c>ActivityMessages</c>).</param>
    void Record(string category, string message);

    /// <summary>
    /// For a message that needs a value the database assigns on insert (for example the id in "ADJ-0004"): the text is
    /// worked out after the change was inserted and the entry is written inside the same transaction.
    /// </summary>
    void Record(string category, Func<string> message);
}
