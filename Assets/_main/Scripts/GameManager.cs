using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // ====================================================
    // REFERENCIAS A LOS PANELES
    // ====================================================
    [Header("Paneles")]
    public GameObject panelMenu;
    public GameObject panelJuego;
    public GameObject panelTabla;

    // ====================================================
    // REFERENCIAS DEL PANEL MENU
    // ====================================================
    [Header("Menu")]
    public TextMeshProUGUI textBienvenida;
    public TextMeshProUGUI textNombreCompleto;

    // ====================================================
    // REFERENCIAS DEL PANEL TABLA
    // ====================================================
    [Header("Tabla de Puntajes")]
    public Transform contentTabla;
    public GameObject filaScorePrefab;
    public Color colorUsuarioActual = new Color(1f, 0.85f, 0f); // amarillo dorado por defecto

    // ====================================================
    // REFERENCIA AL JUEGO
    // ====================================================
    [Header("Juego")]
    public MemoryGame memoryGame;

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Start()
    {
        MostrarPanel("menu");
        StartCoroutine(InicializarMenu());
    }

    IEnumerator InicializarMenu()
    {
        // Esperar a que Firebase esté listo
        yield return new WaitUntil(() => FirebaseManager.Instance != null &&
                                         FirebaseManager.Instance.IsAuthenticated);

        // Mostrar bienvenida con el username
        string username = FirebaseManager.Instance.CurrentUsername;
        if (string.IsNullOrEmpty(username))
        {
            // Si el username no cargó aún, esperar un momento más
            yield return new WaitForSeconds(1f);
            username = FirebaseManager.Instance.CurrentUsername;
        }

        textBienvenida.text = "Bienvenido, " + (string.IsNullOrEmpty(username) ? 
            FirebaseManager.Instance.CurrentUser.Email : username);
    }

    // ====================================================
    // NAVEGACIÓN
    // ====================================================
    void MostrarPanel(string panel)
    {
        panelMenu.SetActive(panel == "menu");
        panelJuego.SetActive(panel == "juego");
        panelTabla.SetActive(panel == "tabla");
    }

    public void OnClickJugar()
    {
        MostrarPanel("juego");
        memoryGame.IniciarJuego();
    }

    public void OnClickVerTabla()
    {
        MostrarPanel("tabla");
        CargarTabla();
    }

    public void OnClickVolverAlMenu()
    {
        MostrarPanel("menu");
    }

    // ====================================================
    // LOGOUT
    // ====================================================
    public void OnClickLogout()
    {
        FirebaseManager.Instance.Logout();
        SceneManager.LoadScene("AuthScene");
    }

    // ====================================================
    // ELIMINAR CUENTA
    // ====================================================
    public void OnClickEliminarCuenta()
    {
        // Mostrar confirmación antes de eliminar
        StartCoroutine(ConfirmarEliminarCuenta());
    }

    IEnumerator ConfirmarEliminarCuenta()
    {
        // Por simplicidad eliminamos directamente
        // En producción mostrarías un diálogo de confirmación
        FirebaseManager.Instance.EliminarCuenta((success, message) =>
        {
            if (success)
            {
                Debug.Log("Cuenta eliminada: " + message);
                SceneManager.LoadScene("AuthScene");
            }
            else
            {
                Debug.LogError("Error al eliminar: " + message);
            }
        });

        yield return null;
    }

    // ====================================================
    // TABLA DE PUNTAJES
    // ====================================================
    void CargarTabla()
    {
        // Limpiar filas anteriores
        foreach (Transform child in contentTabla)
            Destroy(child.gameObject);

        FirebaseManager.Instance.ObtenerTabla((success, tabla) =>
        {
            if (!success || tabla == null) return;

            for (int i = 0; i < tabla.Count; i++)
            {
                GameObject fila = Instantiate(filaScorePrefab, contentTabla);
                TextMeshProUGUI texto = fila.GetComponentInChildren<TextMeshProUGUI>();

                if (texto != null)
                {
                    // Resaltar al usuario actual
                    bool esUsuarioActual = tabla[i].username ==
                        FirebaseManager.Instance.CurrentUsername;

                    texto.text = $"#{i + 1}  {tabla[i].username} — {tabla[i].score} pts";

                    if (esUsuarioActual)
                        texto.color = colorUsuarioActual;
                }
            }
        });
    }

    // Llamado desde MemoryGame cuando termina una partida
    public void OnJuegoTerminado(int score)
    {
        FirebaseManager.Instance.GuardarScore(score, (success, message) =>
        {
            Debug.Log(message);
        });

        // Volver al menú después de un momento
        StartCoroutine(VolverAlMenuConDelay(3f));
    }

    IEnumerator VolverAlMenuConDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        MostrarPanel("menu");
    }
}
