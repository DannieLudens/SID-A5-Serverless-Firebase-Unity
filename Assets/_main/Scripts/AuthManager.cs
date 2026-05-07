using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class AuthManager : MonoBehaviour
{
    // ====================================================
    // REFERENCIAS A LOS PANELES
    // ====================================================
    [Header("Paneles")]
    public GameObject panelLogin;
    public GameObject panelRegistro;
    public GameObject panelRecuperar;

    // ====================================================
    // REFERENCIAS DEL PANEL LOGIN
    // ====================================================
    [Header("Login")]
    public TMP_InputField inputEmailLogin;
    public TMP_InputField inputPasswordLogin;
    public TextMeshProUGUI textMensajeLogin;

    // ====================================================
    // REFERENCIAS DEL PANEL REGISTRO
    // ====================================================
    [Header("Registro")]
    public TMP_InputField inputUsernameRegistro;
    public TMP_InputField inputEmailRegistro;
    public TMP_InputField inputPasswordRegistro;
    public TextMeshProUGUI textMensajeRegistro;

    // ====================================================
    // REFERENCIAS DEL PANEL RECUPERAR
    // ====================================================
    [Header("Recuperar")]
    public TMP_InputField inputEmailRecuperar;
    public TextMeshProUGUI textMensajeRecuperar;

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Start()
    {
        // Mostrar solo el panel de login al inicio
        MostrarPanel("login");

        // Si el usuario ya tiene sesión iniciada, ir directo al juego
        StartCoroutine(VerificarSesion());
    }

    IEnumerator VerificarSesion()
    {
        // Esperar a que Firebase se inicialice (puede tardar un momento)
        yield return new WaitForSeconds(1.5f);

        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsAuthenticated)
        {
            Debug.Log("Sesión activa detectada, redirigiendo al juego...");
            SceneManager.LoadScene("GameScene");
        }
    }

    // ====================================================
    // NAVEGACIÓN ENTRE PANELES
    // ====================================================
    void MostrarPanel(string panel)
    {
        panelLogin.SetActive(panel == "login");
        panelRegistro.SetActive(panel == "registro");
        panelRecuperar.SetActive(panel == "recuperar");
    }

    public void OnClickIrARegistro()
    {
        textMensajeRegistro.text = "";
        MostrarPanel("registro");
    }

    public void OnClickVolverALogin()
    {
        textMensajeLogin.text = "";
        MostrarPanel("login");
    }

    public void OnClickIrARecuperar()
    {
        textMensajeRecuperar.text = "";
        MostrarPanel("recuperar");
    }

    // ====================================================
    // REGISTRO
    // ====================================================
    public void OnClickRegistrar()
    {
        string username = inputUsernameRegistro.text.Trim();
        string email = inputEmailRegistro.text.Trim();
        string password = inputPasswordRegistro.text;

        // Validaciones básicas del lado del cliente
        if (string.IsNullOrEmpty(username))
        {
            textMensajeRegistro.text = "Ingresa un nombre de usuario.";
            return;
        }
        if (string.IsNullOrEmpty(email))
        {
            textMensajeRegistro.text = "Ingresa tu correo electrónico.";
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            textMensajeRegistro.text = "Ingresa una contraseña.";
            return;
        }
        if (password.Length < 6)
        {
            textMensajeRegistro.text = "La contraseña debe tener al menos 6 caracteres.";
            return;
        }

        textMensajeRegistro.text = "Registrando...";

        FirebaseManager.Instance.Registrar(email, password, username, (success, message) =>
        {
            textMensajeRegistro.text = message;

            if (success)
            {
                // Esperar un momento y cargar el juego
                StartCoroutine(IrAlJuegoConDelay(2f));
            }
        });
    }

    // ====================================================
    // LOGIN
    // ====================================================
    public void OnClickLogin()
    {
        string email = inputEmailLogin.text.Trim();
        string password = inputPasswordLogin.text;

        if (string.IsNullOrEmpty(email))
        {
            textMensajeLogin.text = "Ingresa tu correo electrónico.";
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            textMensajeLogin.text = "Ingresa tu contraseña.";
            return;
        }

        textMensajeLogin.text = "Iniciando sesión...";

        FirebaseManager.Instance.Login(email, password, (success, message) =>
        {
            if (success)
            {
                textMensajeLogin.text = "¡Bienvenido!";
                StartCoroutine(IrAlJuegoConDelay(1f));
            }
            else
            {
                textMensajeLogin.text = message;
            }
        });
    }

    // ====================================================
    // RECUPERAR CONTRASEÑA
    // ====================================================
    public void OnClickRecuperar()
    {
        string email = inputEmailRecuperar.text.Trim();

        if (string.IsNullOrEmpty(email))
        {
            textMensajeRecuperar.text = "Ingresa tu correo electrónico.";
            return;
        }

        textMensajeRecuperar.text = "Enviando correo...";

        FirebaseManager.Instance.RecuperarPassword(email, (success, message) =>
        {
            textMensajeRecuperar.text = message;
        });
    }

    // ====================================================
    // UTILIDADES
    // ====================================================
    IEnumerator IrAlJuegoConDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene("GameScene");
    }
}