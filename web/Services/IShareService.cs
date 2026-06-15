namespace QSMPDLE.Web.Services;

public interface IShareService
{
    Task ShareAsync(string text);

    Task ShareToXAsync(string text);

    Task ShareToBlueskyAsync(string text);
}
