public static class NetworkModeData
{
    public enum Mode { Ninguno, Solitario, Host, Cliente }

    //modo seleccionado en el menú
    public static Mode modoSeleccionado = Mode.Ninguno;
    public static string ipDelHost = "127.0.0.1";

    //cooperativo: el P2 siempre es el "Dios" que ayuda al P1
    public static bool modoCooperativo = true;
}
