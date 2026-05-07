using UnityEngine;
using UnityEngine.UI;

public class Carta : MonoBehaviour
{
    private Image imagenCarta;
    private Button botonCarta;

    public int ValorCarta { get; private set; }
    public bool EstaVolteada { get; private set; }
    public bool EstaEncontrada { get; private set; }

    private Color colorFrente;
    private Color colorDorso = new Color(0.3f, 0.3f, 0.35f);
    private MemoryGame juegoMemoria;

    public void Inicializar(int valor, Color color, MemoryGame juego)
    {
        ValorCarta = valor;
        colorFrente = color;
        juegoMemoria = juego;
        EstaVolteada = false;
        EstaEncontrada = false;

        imagenCarta = GetComponent<Image>();
        botonCarta = GetComponent<Button>();

        if (imagenCarta != null)
            imagenCarta.color = colorDorso;

        if (botonCarta != null)
            botonCarta.onClick.AddListener(OnCartaClick);
    }

    void OnCartaClick()
    {
        if (!EstaEncontrada && !EstaVolteada)
            juegoMemoria.CartaSeleccionada(this);
    }

    public void Voltear(bool mostrarFrente)
    {
        EstaVolteada = mostrarFrente;
        if (imagenCarta != null)
            imagenCarta.color = mostrarFrente ? colorFrente : colorDorso;
    }

    public void MarcarComoEncontrada()
    {
        EstaEncontrada = true;
        EstaVolteada = true;
        if (imagenCarta != null)
        {
            Color c = colorFrente;
            c.a = 0.7f;
            imagenCarta.color = c;
        }
        if (botonCarta != null)
            botonCarta.interactable = false;
    }
}