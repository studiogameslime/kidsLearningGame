using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Frame-by-frame sprite animator for UI Images. Takes sprite arrays for each state
/// (Idle, Floating, Success) and cycles through them at a given framerate.
/// </summary>
public class UISpriteAnimator : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] idleFrames;
    public Sprite[] floatingFrames;
    public Sprite[] successFrames;
    public float framesPerSecond = 30f;

    private Sprite[] currentFrames;
    private int currentFrame;
    private float frameTimer;
    private bool isOneShot;
    private Sprite[] oneShotReturnFrames;

    private void Start()
    {
        if (idleFrames != null && idleFrames.Length > 0)
            PlayLoop(idleFrames);
    }

    private void Update()
    {
        if (currentFrames == null || currentFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float interval = 1f / framesPerSecond;

        if (frameTimer >= interval)
        {
            frameTimer -= interval;
            currentFrame++;

            if (currentFrame >= currentFrames.Length)
            {
                if (isOneShot)
                {
                    // Return to previous looping animation
                    isOneShot = false;
                    if (oneShotReturnFrames != null && oneShotReturnFrames.Length > 0)
                        PlayLoop(oneShotReturnFrames);
                    return;
                }
                currentFrame = 0;
            }

            if (targetImage != null && currentFrame < currentFrames.Length)
                targetImage.sprite = currentFrames[currentFrame];
        }
    }

    public void PlayIdle()
    {
        if (idleFrames != null && idleFrames.Length > 0)
            PlayLoop(idleFrames);
    }

    public void PlayFloating()
    {
        if (floatingFrames != null && floatingFrames.Length > 0)
            PlayLoop(floatingFrames);
    }

    public void PlaySuccess()
    {
        if (successFrames != null && successFrames.Length > 0)
        {
            isOneShot = true;
            oneShotReturnFrames = idleFrames;
            currentFrames = successFrames;
            currentFrame = 0;
            frameTimer = 0f;
            if (targetImage != null) targetImage.sprite = successFrames[0];
        }
    }

    private void PlayLoop(Sprite[] frames)
    {
        isOneShot = false;
        currentFrames = frames;
        currentFrame = 0;
        frameTimer = 0f;
        if (targetImage != null && frames.Length > 0)
            targetImage.sprite = frames[0];
    }
}
