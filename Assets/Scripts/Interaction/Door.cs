using UnityEngine;

/// <summary>
/// Puerta que gira sobre su eje Y local (este objeto es la bisagra).
/// El jugador la abre o cierra llamando a Toggle().
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Door : MonoBehaviour
{
    [Tooltip("Ángulo de apertura en grados. Negativo o positivo según el lado hacia el que abre.")]
    public float openAngle = -95f;
    [Tooltip("Segundos que tarda en abrirse o cerrarse por completo.")]
    public float duration = 0.8f;
    public AudioSource sound;

    public bool IsOpen { get; private set; }

    private Rigidbody rb;
    private Quaternion closedRotation;
    private float progress;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        closedRotation = transform.localRotation;
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (sound != null) sound.Play();
    }

    void FixedUpdate()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(progress, target)) return;

        progress = Mathf.MoveTowards(progress, target, Time.fixedDeltaTime / Mathf.Max(0.01f, duration));
        Quaternion local = closedRotation * Quaternion.Euler(0f, openAngle * Mathf.SmoothStep(0f, 1f, progress), 0f);
        Quaternion world = transform.parent != null ? transform.parent.rotation * local : local;
        rb.MoveRotation(world);
    }
}
