using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MemoryGame : MonoBehaviour
{
    // ====================================================
    // CONFIGURACIÓN
    // ====================================================
    [Header("Configuración")]
    public int totalPares = 8; // 8 pares = 16 cartas

    [Header("UI")]
    public TextMeshProUGUI textScore;
    public TextMeshProUGUI textTiempo;
    public TextMeshProUGUI textMensaje;
    public Transform gridCartas;
    public GameObject cartaPrefab;

    [Header("Colores de las cartas")]
    public Color[] coloresCartas; // Asignar 8 colores diferentes en el Inspector

    // ====================================================
    // VARIABLES INTERNAS
    // ====================================================
    private List<Carta> cartasEnJuego = new List<Carta>();
    private Carta primeraCarta = null;
    private Carta segundaCarta = null;
    private bool puedoVoltear = true;

    private int paresEncontrados = 0;
    private int intentos = 0;
    private int score = 0;
    private float tiempoJuego = 0f;
    private bool juegoActivo = false;

    // Referencia al GameManager
    private GameManager gameManager;

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // Si no se asignaron colores, usar colores por defecto
        if (coloresCartas == null || coloresCartas.Length < totalPares)
        {
            coloresCartas = new Color[]
            {
                new Color(0.9f, 0.2f, 0.2f),  // Rojo
                new Color(0.2f, 0.6f, 0.9f),  // Azul
                new Color(0.2f, 0.8f, 0.2f),  // Verde
                new Color(0.9f, 0.8f, 0.1f),  // Amarillo
                new Color(0.8f, 0.2f, 0.8f),  // Morado
                new Color(0.9f, 0.5f, 0.1f),  // Naranja
                new Color(0.1f, 0.8f, 0.7f),  // Cyan
                new Color(0.9f, 0.4f, 0.6f),  // Rosa
            };
        }
    }

    void Update()
    {
        if (!juegoActivo) return;

        tiempoJuego += Time.deltaTime;
        if (textTiempo != null)
            textTiempo.text = $"Tiempo: {(int)tiempoJuego}s";
    }

    // ====================================================
    // INICIAR JUEGO
    // ====================================================
    public void IniciarJuego()
    {
        // Limpiar cartas anteriores
        foreach (Transform child in gridCartas)
            Destroy(child.gameObject);

        cartasEnJuego.Clear();
        primeraCarta = null;
        segundaCarta = null;
        puedoVoltear = true;
        paresEncontrados = 0;
        intentos = 0;
        score = 0;
        tiempoJuego = 0f;
        juegoActivo = true;

        ActualizarUI();
        textMensaje.text = "";

        // Crear las cartas
        CrearCartas();
    }

    void CrearCartas()
    {
        // Crear lista de valores (cada valor aparece 2 veces = un par)
        List<int> valores = new List<int>();
        for (int i = 0; i < totalPares; i++)
        {
            valores.Add(i);
            valores.Add(i);
        }

        // Mezclar aleatoriamente (algoritmo Fisher-Yates)
        for (int i = valores.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = valores[i];
            valores[i] = valores[j];
            valores[j] = temp;
        }

        // Crear cada carta en el grid
        for (int i = 0; i < valores.Count; i++)
        {
            GameObject cartaObj = Instantiate(cartaPrefab, gridCartas);
            Carta carta = cartaObj.GetComponent<Carta>();

            if (carta != null)
            {
                carta.Inicializar(valores[i], coloresCartas[valores[i]], this);
                cartasEnJuego.Add(carta);
            }
        }
    }

    // ====================================================
    // LÓGICA DEL JUEGO
    // ====================================================
    // Llamado cuando el jugador voltea una carta
    public void CartaSeleccionada(Carta carta)
    {
        if (!puedoVoltear) return;
        if (carta == primeraCarta) return; // No puede seleccionar la misma carta dos veces
        if (carta.EstaVolteada) return;

        carta.Voltear(true);

        if (primeraCarta == null)
        {
            // Primera carta seleccionada
            primeraCarta = carta;
        }
        else
        {
            // Segunda carta seleccionada
            segundaCarta = carta;
            intentos++;
            puedoVoltear = false;

            StartCoroutine(VerificarPar());
        }
    }

    IEnumerator VerificarPar()
    {
        yield return new WaitForSeconds(0.8f);

        if (primeraCarta.ValorCarta == segundaCarta.ValorCarta)
        {
            // ¡Par encontrado!
            primeraCarta.MarcarComoEncontrada();
            segundaCarta.MarcarComoEncontrada();
            paresEncontrados++;

            // Calcular puntos: más puntos por menos intentos y menos tiempo
            int puntosPorPar = Mathf.Max(10, 50 - intentos * 2);
            score += puntosPorPar;

            textMensaje.text = "¡Par encontrado! +" + puntosPorPar;

            // Verificar si ganó
            if (paresEncontrados == totalPares)
            {
                StartCoroutine(JuegoCompletado());
            }
        }
        else
        {
            // No es par — voltear de regreso
            primeraCarta.Voltear(false);
            segundaCarta.Voltear(false);
            textMensaje.text = "Intenta de nuevo...";
        }

        primeraCarta = null;
        segundaCarta = null;
        puedoVoltear = true;

        ActualizarUI();
    }

    IEnumerator JuegoCompletado()
    {
        juegoActivo = false;

        // Bonus por tiempo: más rápido = más puntos
        int bonusTiempo = Mathf.Max(0, 200 - (int)tiempoJuego * 2);
        score += bonusTiempo;

        textMensaje.text = $"¡Completado!\nScore: {score}\nTiempo: {(int)tiempoJuego}s\nBonus tiempo: +{bonusTiempo}";
        ActualizarUI();

        yield return new WaitForSeconds(2f);

        // Enviar score al GameManager para guardarlo en Firebase
        if (gameManager != null)
            gameManager.OnJuegoTerminado(score);
    }

    void ActualizarUI()
    {
        if (textScore != null)
            textScore.text = $"Score: {score} | Pares: {paresEncontrados}/{totalPares} | Intentos: {intentos}";
    }
}
