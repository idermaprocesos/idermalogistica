namespace IdermaFichas.Services;

public enum ObjetivoTour
{
    ElementoVentana,
    ElementoPagina,
    ItemMenu,
    ItemConfiguracion
}

public sealed record PasoTour(
    string Titulo,
    string Texto,
    string? Destino,
    ObjetivoTour Objetivo,
    string? Nombre,
    bool ExpandirDashboards = false);

public sealed record GuiaSeccionTour(
    string Id,
    string Titulo,
    string Grupo,
    IReadOnlyList<PasoTour> Pasos);

public static class TourTrabajadorNuevo
{
    public static IReadOnlyList<PasoTour> Pasos { get; } =
    [
        new(
            "Recorrido para personal nuevo",
            "Le vamos a mostrar el menú, las secciones de trabajo y esta pantalla de configuración. Use Siguiente para avanzar o Salir para cortar el recorrido cuando quiera.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAyudaTrabajador"),
        new(
            "Menú lateral",
            "Aquí están todas las secciones. Pulse el icono de las tres líneas en la barra superior si el menú está cerrado. El engranaje de abajo abre Configuración.",
            "inicio",
            ObjetivoTour.ItemMenu,
            "inicio"),
        new(
            "Búsqueda",
            "Escriba el nombre de un producto: saldrá en catálogo, vencimientos, registro e historial de precios si está en esas listas. Pulse Enter o elija un resultado para ir directo.",
            "inicio",
            ObjetivoTour.ElementoVentana,
            "CajaBusquedaGlobal"),
        new(
            "Inicio",
            "Es el tablero del día: totales, vencidos, por vencer y stock bajo. Conviene empezar aquí al abrir el programa.",
            "inicio",
            ObjetivoTour.ItemMenu,
            "inicio"),
        new(
            "Resumen de Inicio",
            "Estas tarjetas resumen el inventario. Pulse las de caducidad o inventario para entrar a esa lista.",
            "inicio",
            ObjetivoTour.ElementoPagina,
            "PanelKpis"),
        new(
            "Inventario",
            "Lista de todas las fichas técnicas, sin filtrar por área. Desde aquí puede buscar, exportar o abrir un producto.",
            "catalogo",
            ObjetivoTour.ItemMenu,
            "catalogo"),
        new(
            "Lista del inventario",
            "Use los botones de la derecha para exportar o borrar. Pulse una fila para abrir la ficha en su área.",
            "catalogo",
            ObjetivoTour.ElementoPagina,
            "TituloPagina"),
        new(
            "Vencimientos",
            "Materiales vencidos o por caducar, por lote. Sirve para retirar o reponer a tiempo.",
            "caducidad",
            ObjetivoTour.ItemMenu,
            "caducidad"),
        new(
            "Registro",
            "Historial de ingresos y salidas por producto y unidad. Aquí se controla el movimiento de inventario.",
            "registro",
            ObjetivoTour.ItemMenu,
            "registro"),
        new(
            "Historial de precios",
            "Lista de productos con los mismos filtros del catálogo. Al elegir uno se ve y se edita su historial de precios en soles o dólares.",
            "historial-precios",
            ObjetivoTour.ItemMenu,
            "historial-precios"),
        new(
            "Proveedores",
            "Debajo de Orden de compra. Registre proveedores, califíquelos (puntual, problemas, retraso…) y deje notas. Puede filtrar por el mes o el año en que se agregaron.",
            "proveedores",
            ObjetivoTour.ItemMenu,
            "proveedores"),
        new(
            "Áreas operativas",
            "Dentro del desplegable Inventario, cada categoría (Activos fijos, Insumos médicos, etc.) agrupa sus secciones alineadas con el resto del menú. Nombres, iconos y encabezados se cambian en Configuración y se guardan en Firestore.",
            "primera-area",
            ObjetivoTour.ItemMenu,
            "primera-area"),
        new(
            "Productos del área",
            "En el área se dan de alta fichas, se editan existencias y se imprimen o exportan. El código usa el prefijo del área (por ejemplo CDE_001).",
            "primera-area",
            ObjetivoTour.ElementoPagina,
            "TituloArea"),
        new(
            "Generador de etiquetas",
            "Importa un Excel, elige el tipo de código y genera la cuadrícula de etiquetas.",
            "etiquetas",
            ObjetivoTour.ItemMenu,
            "etiquetas"),
        new(
            "Acerca de",
            "Datos del programa y de este equipo. Si hay un problema técnico, esta pantalla ayuda a identificar la versión.",
            "acerca",
            ObjetivoTour.ItemMenu,
            "acerca"),
        new(
            "Configuración",
            "El engranaje del menú abre los ajustes agrupados: ayuda, estructura del inventario, guardado, datos y zona delicada. Siguiente recorre cada bloque.",
            "configuracion",
            ObjetivoTour.ItemConfiguracion,
            null),
        new(
            "Troubleshooting",
            "Comprueba si este equipo entra a la base de datos. Si falla, muestra el error y cómo corregirlo.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaTroubleshooting"),
        new(
            "Áreas en Configuración",
            "Aquí se crean categorías y secciones, se elige icono, prefijo y formulario, y se puede mover una sección de una categoría a otra. Las flechas cambian el orden del menú.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAreas"),
        new(
            "Desplegables",
            "Listas de los formularios: categoría, unidad, color, etc. Elija el área, luego el desplegable, y agregue o edite valores.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaDesplegables"),
        new(
            "Medicación común",
            "Importe un Excel con medicamentos frecuentes. En Recetas aparecerán como sugerencia al escribir el nombre.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaMedicacion"),
        new(
            "Autoguardado",
            "Si edita una ficha, se guarda sola a los 15 segundos. El intervalo extra es un respaldo. Déjelo activado en el día a día.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAutoguardado"),
        new(
            "Copias de seguridad",
            "Guarda fichas, áreas, RR. HH. y el registro. Puede crear una copia ahora, restaurar una anterior o abrir la carpeta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaCopias"),
        new(
            "Exportar información",
            "Descarga un ZIP con lo que marque: fichas, fotos, registro, preferencias. Útil para llevar datos a otro equipo.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaExportar"),
        new(
            "Archivo e importación",
            "Aquí ve la ruta de los datos. Puede importar Excel o JSON, descargar la plantilla o restaurar los ejemplos de la clínica.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaArchivoDatos"),
        new(
            "Borrar base de datos",
            "Vacía el catálogo. Antes se crea una copia con fecha. No lo use en el trabajo diario; pida apoyo si hace falta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaBorrarBase"),
        new(
            "Listo para trabajar",
            "Ya vio el menú y la configuración. Puede repetir este recorrido cuando quiera desde esta misma tarjeta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAyudaTrabajador")
    ];

    public static IReadOnlyList<GuiaSeccionTour> GuiasPorSeccion { get; } =
    [
        new("catalogo", "Inventario", "Inventario",
        [
            new("Inventario", "Esta sección lista todas las fichas técnicas de la clínica, sin filtrar por área. Es el catálogo completo.", "catalogo", ObjetivoTour.ItemMenu, "catalogo"),
            new("Qué verá aquí", "Arriba está el título y un resumen de cuántos productos hay. A la derecha, la barra de acciones para actualizar, exportar, borrar, enviar a merma o mover de área.", "catalogo", ObjetivoTour.ElementoPagina, "TituloPagina"),
            new("Filtros rápidos", "Los chips de stock crítico, vencidos y próximos 30 días acotan la lista al instante. También puede filtrar por área con los chips de color.", "catalogo", ObjetivoTour.ElementoPagina, "ChipStockCritico"),
            new("Buscar y ordenar", "Escriba código, nombre, marca o proveedor. Use el área y el orden para organizar la lista antes de abrir o exportar.", "catalogo", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Filtros avanzados", "Stock, caducidad, categoría, proveedor, ubicación y lote afinan la búsqueda cuando hay mucho material.", "catalogo", ObjetivoTour.ElementoPagina, "ComboStock"),
            new("Lista de productos", "Pulse una fila para abrir la ficha en su área. Con «Seleccionar» puede marcar varias y exportar, borrar o enviar a merma juntas.", "catalogo", ObjetivoTour.ElementoPagina, "ListaCatalogo")
        ]),
        new("registro", "Registro", "Inventario",
        [
            new("Registro", "Aquí se controlan ingresos y salidas por producto y por unidad. Es el historial de movimiento del inventario.", "registro", ObjetivoTour.ItemMenu, "registro"),
            new("Unidades y alertas", "Elija la unidad (clínica, almacén…) y use los chips de stock crítico, vencidos o próximos para priorizar qué revisar.", "registro", ObjetivoTour.ElementoPagina, "TituloPagina"),
            new("Buscar productos", "Busque por código o nombre. El año, los meses y el orden ayudan a ver el periodo que le interesa.", "registro", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Filtros del listado", "Stock, caducidad, categoría, proveedor, ubicación y lote dejan solo los productos que necesita.", "registro", ObjetivoTour.ElementoPagina, "ComboStock"),
            new("Lista e historial", "Al elegir un producto se abre su historial de movimientos a la derecha: ingresos en verde y salidas en rojo, con totales del periodo.", "registro", ObjetivoTour.ElementoPagina, "ListaProductos"),
            new("Registrar movimiento", "Abajo puede cargar un ingreso o una salida: cantidad, documento, partida, lote, caducidad, nota y recibo adjunto. Guarde con el botón de registrar.", "registro", ObjetivoTour.ElementoPagina, "PanelIngreso")
        ]),
        new("caducidad", "Vencimientos", "Inventario",
        [
            new("Vencimientos", "Materiales ya vencidos o próximos a caducar, agrupados por lote. Sirve para retirar o reponer a tiempo.", "caducidad", ObjetivoTour.ItemMenu, "caducidad"),
            new("Horizonte", "Elija cuántos días hacia adelante cuenta como «próximo a vencer» (por ejemplo 30 o 60 días).", "caducidad", ObjetivoTour.ElementoPagina, "ComboHorizonte"),
            new("Resumen", "Las tarjetas muestran cuántos lotes están vencidos y cuántos caducan dentro del horizonte elegido.", "caducidad", ObjetivoTour.ElementoPagina, "ConteoVencidos"),
            new("Lista de vencidos", "A la izquierda: productos cuyo lote ya no debe usarse. Pulse uno para ir a su ficha o al registro.", "caducidad", ObjetivoTour.ElementoPagina, "ListaVencidos"),
            new("Lista de próximos", "A la derecha: lotes que caducan pronto. Revíselos para planificar reposición o merma.", "caducidad", ObjetivoTour.ElementoPagina, "ListaProximos")
        ]),
        new("orden-compra", "Orden de compra", "Gestión",
        [
            new("Orden de compra", "Desde aquí marca productos del inventario y genera la orden mensual de compra.", "orden-compra", ObjetivoTour.ItemMenu, "orden-compra"),
            new("Filtros rápidos", "Unidades, stock crítico, vencidos y próximos acotan qué productos conviene pedir.", "orden-compra", ObjetivoTour.ElementoPagina, "ChipStockCritico"),
            new("Búsqueda y periodo", "Busque productos y elija año o meses. El consumo del periodo ayuda a decidir cantidades.", "orden-compra", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Filtros de catálogo", "Stock, caducidad, categoría, proveedor, ubicación y lote dejan lista lista para marcar.", "orden-compra", ObjetivoTour.ElementoPagina, "ComboStock"),
            new("Generar la orden", "Con productos marcados, use «Generar orden de compra». También puede editar borradores o reabrir órdenes anteriores.", "orden-compra", ObjetivoTour.ElementoPagina, "TituloLista"),
            new("Marcar productos", "Active «Seleccionar» o «Marcar lista» para elegir varias filas. Luego genere la orden con lo marcado.", "orden-compra", ObjetivoTour.ElementoPagina, "BotonModoSeleccionar")
        ]),
        new("orden-servicio", "Orden de servicio", "Gestión",
        [
            new("Orden de servicio", "Sección distinta de la orden de compra: amplía contratos de proveedores por meses.", "orden-servicio", ObjetivoTour.ItemMenu, "orden-servicio"),
            new("Buscar y filtrar", "Busque por proveedor, RUC o razón social. Filtre con o sin contrato según lo que necesite.", "orden-servicio", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Filtro de contrato", "«Con contrato» o «Sin contrato» deja solo los proveedores que encajan con su tarea.", "orden-servicio", ObjetivoTour.ElementoPagina, "FiltroContrato"),
            new("Generar orden", "Marque proveedores en la lista y use «Generar orden de servicio». También hay borradores y órdenes anteriores.", "orden-servicio", ObjetivoTour.ElementoPagina, "Resumen"),
            new("Lista de proveedores", "Cada fila muestra nombre, empresa, RUC y estado del contrato. Marque los que entrarán en la orden.", "orden-servicio", ObjetivoTour.ElementoPagina, "Lista")
        ]),
        new("historial-precios", "Historial de precios", "Gestión",
        [
            new("Historial de precios", "Consulte y edite el historial de precios de cada producto en soles o dólares.", "historial-precios", ObjetivoTour.ItemMenu, "historial-precios"),
            new("Filtros", "Misma lógica que el inventario: búsqueda, área, orden, stock, caducidad, categoría, proveedor, ubicación y lote.", "historial-precios", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Lista de productos", "Elija un producto a la izquierda. A la derecha verá su historial y podrá cargar un precio nuevo.", "historial-precios", ObjetivoTour.ElementoPagina, "ListaProductos"),
            new("Detalle del producto", "Al seleccionar uno aparecen título, subtítulo y el panel para registrar fecha, monto, moneda y nota.", "historial-precios", ObjetivoTour.ElementoPagina, "TituloProducto"),
            new("Formulario de precio", "Indique fecha, monto y moneda (soles o dólares). Guarde para dejar constancia de la variación.", "historial-precios", ObjetivoTour.ElementoPagina, "PanelFormulario")
        ]),
        new("notificaciones", "Notificaciones", "Gestión",
        [
            new("Notificaciones", "Solicitudes de clínica, incidencias y autorizaciones de borrado. El badge del menú avisa si hay pendientes.", "notificaciones", ObjetivoTour.ItemMenu, "notificaciones"),
            new("Conteo", "Arriba verá cuántas notificaciones hay. Revise siempre las pendientes al empezar el turno.", "notificaciones", ObjetivoTour.ElementoPagina, "TextoConteo"),
            new("Buscar y filtrar", "Busque por folio, persona, producto o motivo. El estado (pendiente, aceptada…) acota la lista.", "notificaciones", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Solicitudes", "En la primera columna están las solicitudes de clínica: puede aceptarlas o denegarlas según el protocolo.", "notificaciones", ObjetivoTour.ElementoPagina, "ListaNotificaciones"),
            new("Incidencias", "La columna central lista reportes de incidencias para dar seguimiento.", "notificaciones", ObjetivoTour.ElementoPagina, "ListaIncidencias"),
            new("Autorizaciones", "A la derecha: pedidos de autorización para borrar del inventario. Apruebe o rechace con cuidado.", "notificaciones", ObjetivoTour.ElementoPagina, "ListaAutorizaciones")
        ]),
        new("proveedores", "Proveedores", "Gestión",
        [
            new("Proveedores", "Alta y seguimiento de proveedores: calificación, notas y filtros por fecha de alta.", "proveedores", ObjetivoTour.ItemMenu, "proveedores"),
            new("Título y conteo", "Aquí gestiona la agenda de proveedores. El conteo indica cuántos hay con los filtros actuales.", "proveedores", ObjetivoTour.ElementoPagina, "TituloPagina"),
            new("Buscar y calificar", "Busque por nombre o empresa. Filtre por calificación (puntual, problemas, retraso…) para priorizar.", "proveedores", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Filtro por fecha", "Año y meses (o últimos 3/6/12) muestran solo los agregados en ese periodo.", "proveedores", ObjetivoTour.ElementoPagina, "ComboAnio"),
            new("Lista", "Pulse un proveedor para ver o editar datos, calificación y notas. Use el alta nueva cuando llegue un proveedor.", "proveedores", ObjetivoTour.ElementoPagina, "Lista")
        ]),
        new("pronostico", "Pronóstico", "Gestión",
        [
            new("Pronóstico", "Compara ingresos, salidas y merma del rango elegido con el periodo anterior de la misma duración.", "pronostico", ObjetivoTour.ItemMenu, "pronostico"),
            new("Rango y área", "Elija el rango (hoy, semana, 30 días…) y el área. «Personalizado» activa fechas desde/hasta.", "pronostico", ObjetivoTour.ElementoPagina, "ComboPeriodo"),
            new("Orden y búsqueda", "Ordene por más mermas, ingresos o salidas. Busque por código o nombre y exporte a PDF si lo necesita.", "pronostico", ObjetivoTour.ElementoPagina, "CajaBusqueda"),
            new("Indicadores", "Ingresos, salidas, merma, productos que subieron y que bajaron respecto al periodo anterior.", "pronostico", ObjetivoTour.ElementoPagina, "KpiIngresos"),
            new("Detalle por producto", "La lista muestra el comportamiento de cada ítem. La demanda son las salidas que no son merma.", "pronostico", ObjetivoTour.ElementoPagina, "Lista")
        ]),
        new("etiquetas", "Generador de etiquetas", "Control",
        [
            new("Generador de etiquetas", "Importa un Excel o ingresos del registro, elige el tipo de código y genera la cuadrícula para imprimir.", "etiquetas", ObjetivoTour.ItemMenu, "etiquetas"),
            new("Qué hace esta pantalla", "Adjunte un Excel o importe del registro, configure el código y revise la vista previa antes de generar.", "etiquetas", ObjetivoTour.ElementoPagina, "TituloPagina"),
            new("Productos a etiquetar", "Cargue el archivo o importe ingresos. Verá el nombre del archivo y un resumen de filas leídas.", "etiquetas", ObjetivoTour.ElementoPagina, "NombreArchivo"),
            new("Opciones de impresión", "Tipo de código, hoja, columnas, filas, disposición, guía de corte y tamaño del código en milímetros.", "etiquetas", ObjetivoTour.ElementoPagina, "ComboTipo"),
            new("Generar", "Cuando la configuración esté lista, pulse Generar. A la derecha verá la vista previa de cada etiqueta.", "etiquetas", ObjetivoTour.ElementoPagina, "BotonGenerar")
        ]),
        new("mantenimiento", "Mantenimiento", "Control",
        [
            new("Mantenimiento", "Control de plazos de equipos. Logística lleva el seguimiento; el reporte detallado está en la app de Mantenimiento.", "mantenimiento", ObjetivoTour.ItemMenu, "mantenimiento"),
            new("Filtro", "Vea todos, solo preventivo, correctivo, crítico, o los vencidos y por vencer.", "mantenimiento", ObjetivoTour.ElementoPagina, "ComboFiltro"),
            new("Resumen", "El texto resume cuántos equipos hay según el filtro. Use «Agregar equipo» para dar de alta uno nuevo.", "mantenimiento", ObjetivoTour.ElementoPagina, "TextoResumen"),
            new("Lista de equipos", "Cada fila muestra el equipo y su plazo. Desde aquí abre el detalle o marca el mantenimiento hecho.", "mantenimiento", ObjetivoTour.ElementoPagina, "Lista")
        ]),
        new("merma", "Merma y Depurados", "Control",
        [
            new("Merma y Depurados", "Productos retirados del inventario por vencimiento, daño u otro motivo. Puede corregir cantidades o revertir.", "merma", ObjetivoTour.ItemMenu, "merma"),
            new("Periodo", "Filtre por hoy, ayer, esta semana, este mes o un rango personalizado con fechas desde/hasta.", "merma", ObjetivoTour.ElementoPagina, "ComboPeriodo"),
            new("Lista de merma", "Cada ítem muestra qué se retiró y cuánto. Edite la cantidad o revierta: el inventario y el registro se ajustan solos.", "merma", ObjetivoTour.ElementoPagina, "Lista")
        ])
    ];

    public static GuiaSeccionTour? ObtenerGuia(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return GuiasPorSeccion.FirstOrDefault(g =>
            g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
