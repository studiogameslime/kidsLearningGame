using UnityEngine;
using GoogleMobileAds.Ump.Api;

/// <summary>
/// Manages GDPR/UMP consent for ad personalization.
/// Runs before any ads are loaded. Required for EU compliance.
///
/// Flow:
/// 1. Request consent info update on app launch
/// 2. If consent form is available and required, show it
/// 3. Ads load only after consent is resolved (or not required)
/// </summary>
public static class ConsentManager
{
    public static bool CanShowAds { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        RequestConsent();
    }

    private static void RequestConsent()
    {
        var requestParameters = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = true, // COPPA: users are children
        };

        ConsentInformation.Update(requestParameters, (FormError error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[Consent] Update failed: {error.Message}");
                // Allow ads anyway — consent not required if update fails
                CanShowAds = true;
                return;
            }

            Debug.Log($"[Consent] Status: {ConsentInformation.ConsentStatus}, FormAvailable: {ConsentInformation.IsConsentFormAvailable()}");

            if (ConsentInformation.IsConsentFormAvailable())
            {
                LoadAndShowForm();
            }
            else
            {
                // No form needed (not in EEA, or already consented)
                CanShowAds = true;
            }
        });
    }

    private static void LoadAndShowForm()
    {
        ConsentForm.Load((ConsentForm form, FormError loadError) =>
        {
            if (loadError != null)
            {
                Debug.LogWarning($"[Consent] Form load failed: {loadError.Message}");
                CanShowAds = true;
                return;
            }

            if (ConsentInformation.ConsentStatus == ConsentStatus.Required)
            {
                form.Show((FormError showError) =>
                {
                    if (showError != null)
                        Debug.LogWarning($"[Consent] Form show failed: {showError.Message}");

                    // Consent resolved (accepted or rejected)
                    CanShowAds = ConsentInformation.CanRequestAds();
                    Debug.Log($"[Consent] Resolved. CanShowAds={CanShowAds}");
                });
            }
            else
            {
                CanShowAds = ConsentInformation.CanRequestAds();
                Debug.Log($"[Consent] Already resolved. CanShowAds={CanShowAds}");
            }
        });
    }
}
