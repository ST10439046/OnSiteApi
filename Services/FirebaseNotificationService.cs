using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace OnSiteApi.Services;

public class FirebaseNotificationService
{
    private readonly ILogger<FirebaseNotificationService> _logger;

    public FirebaseNotificationService(
        IConfiguration configuration,
        ILogger<FirebaseNotificationService> logger)
    {
        _logger = logger;

        var credentialsJson =
            configuration["FIREBASE_SERVICE_ACCOUNT_JSON"];

        if (string.IsNullOrWhiteSpace(credentialsJson))
        {
            _logger.LogWarning(
                "FIREBASE_SERVICE_ACCOUNT_JSON is not configured. FCM sending is disabled.");
            
            return;
        }

        if (
            FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(
                new AppOptions
                {
                    Credential =
                        GoogleCredential
                            .FromJson(credentialsJson)
                });
        }
    }

    public async Task<bool> SendNotificationAsync(
        string token,
        string title,
        string message,
        Dictionary<string, string>? data = null)
    {
        if (
            string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (
            FirebaseApp.DefaultInstance == null)
        {
            _logger.LogWarning(
                "Firebase is not configured. Notification was not sent.");

            return false;
        }

        try
        {
            var notification =
                new Message
                {
                    Token = token,

                    Notification =
                        new Notification
                        {
                            Title = title,
                            Body = message
                        },

                    Data =
                        data ??
                        new Dictionary<string, string>()
                };

            var response =
                await FirebaseMessaging.DefaultInstance
                    .SendAsync(notification);

            _logger.LogInformation(
                "FCM notification sent successfully. Message ID: {MessageId}",
                response);

            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(
                ex,
                "Firebase rejected notification for token.");

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending FCM notification.");

            return false;
        }
    }

    public async Task<int> SendToTokensAsync(
        IEnumerable<string> tokens,
        string title,
        string message,
        Dictionary<string, string>? data = null)
    {
        var tokenList =
            tokens
                .Where(t =>
                    !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

        var sentCount = 0;

        foreach (var token in tokenList)
        {
            if (
                await SendNotificationAsync(
                    token,
                    title,
                    message,
                    data))
            {
                sentCount++;
            }
        }

        return sentCount;
    }
}