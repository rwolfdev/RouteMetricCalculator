using Spectre.Console;

Console.WriteLine("Route Metric Calculator (RMC)");
Console.WriteLine("Developed by Robert Wolf\n");

List<Route> routes = new List<Route>();
if (args.Length == 0)
{
    Console.WriteLine("This program needs a file which contains the routes which should be calculated, the format should be\n\n<route_source> <route_destination> <costs>\n\nUse a new line for each new route!");
    return;
}
else
{
    if (File.Exists(args[0]))
    {
        string[] lines = File.ReadAllLines(args[0]);

        foreach (string line in lines)
        {
            string[] values = line.Split(' ');

            if (values.Length == 3)
            {
                routes.Add(new Route()
                {
                    SourceRouterName = values[0],
                    DestinationRouterName = values[1],
                    Cost = Int32.Parse(values[2]),
                });
            }
        }
    }
    else
    {
        Console.WriteLine("File was not found!\n\nThis program needs a file which contains the routes which should be calculated, the format should be\n\n<route_source> <route_destination> <costs>\n\nUse a new line for each new route!");
        return;
    }
}

List<string> routerNames = new List<string>();

routerNames.AddRange(routes.Select(r => r.SourceRouterName));
routerNames.AddRange(routes.Select(r => r.DestinationRouterName));
routerNames = routerNames.Distinct().ToList();

List<RouterMap> maps = new List<RouterMap>();
Dictionary<int, List<RouterMap>> timeslot_maps = new Dictionary<int, List<RouterMap>>();

foreach (string routerName in routerNames)
{
    RouterMap map = new RouterMap();
    map.RouterName = routerName;
    foreach (Route route in routes)
    {
        if (route.SourceRouterName == routerName || route.DestinationRouterName == routerName)
        {
            map.AddNewRoute(route);
        }
    }

    maps.Add(map);
}

int timeslot = 0;
// Initial states of the routing tables
timeslot_maps.Add(timeslot, maps);

// solange im aktuellen zeitschritt neue routen gelernt wurden -> zähle Zeitschritt nach oben und übergebe Nachbarrouten an Router
while (timeslot_maps.Where(k => k.Key == timeslot) // aktueller Timeslot
        .Where(v => v.Value.Where(m => m.LearnedNewRoutes).Count() > 0) // wo die Maps neue Routen gelernt haben
        .Count() > 0)
{
    maps = new List<RouterMap>();
    KeyValuePair<int, List<RouterMap>> kvp = timeslot_maps.Where(k => k.Key == timeslot).FirstOrDefault();

    foreach (RouterMap map in kvp.Value)
    {
        RouterMap new_map = new RouterMap();
        // vorhande Routen werden übernommen
        new_map.KnownRoutes = new List<Route>();
        new_map.KnownRoutes.AddRange(map.KnownRoutes);
        new_map.RouterName = map.RouterName;

        // für jeden direkten nachbarn
        foreach (Route routenNachbar in new_map.KnownRoutes.Where(r => String.IsNullOrEmpty(r.ViaRouter)).ToArray())
        {
            // Destination is der nachbar, da kein Via gesetzt ist
            RouterMap nachbar_map = kvp.Value.Where(r => r.RouterName == routenNachbar.DestinationRouterName).FirstOrDefault();

            // Durchlaufe jede nachbar route, wo er nicht das Ziel ist.
            foreach (Route nachbar_route in nachbar_map.KnownRoutes.Where(r => r.DestinationRouterName != new_map.RouterName).ToArray())
            {
                Route route = new Route()
                {
                    DestinationRouterName = nachbar_route.DestinationRouterName, // Ziel bleibt der selbe Router
                    SourceRouterName = new_map.RouterName, // er selbst wird zur Source
                    ViaRouter = nachbar_map.RouterName, // Nachbar wird zum ViaRouter
                    Cost = nachbar_route.Cost + routenNachbar.Cost // Kosten des Nachbarn + die Kosten zum Nachbarn ergeben neue Kosten,
                };
                new_map.AddNewRoute(route);
            }
        }
        maps.Add(new_map);
    }
    timeslot = timeslot + 1;
    timeslot_maps.Add(timeslot, maps);
}

foreach (KeyValuePair<int, List<RouterMap>> kvp in timeslot_maps)
{
    foreach (RouterMap map in kvp.Value.OrderBy(m => m.RouterName))
    {
        ShowTimeSlotTable(map, kvp.Key);        
    }
}



void ShowTimeSlotTable(RouterMap map, int timeslot)
{
    AnsiConsole.WriteLine("Router map for router: " + map.RouterName + " for time t" + timeslot);

    Table table = new Table().Centered();

    table.AddColumn(new TableColumn("To (Destination)"));
    table.AddColumn(new TableColumn("Via router"));
    table.AddColumn(new TableColumn("Costs"));

    foreach (Route route in map.KnownRoutes.OrderBy(r => r.DestinationRouterName))
    {
        table.AddRow(route.DestinationRouterName, route.ViaRouter ?? "-", route.Cost.ToString());
    }

    AnsiConsole.Write(table);
}

class RouterMap
{
    public RouterMap()
    {
        LearnedNewRoutes = false;
    }

    public string RouterName { get; set; }
    public List<Route> KnownRoutes { get; set; }
    public bool LearnedNewRoutes { get; set; }

    public void AddNewRoute(Route route)
    {
        if (KnownRoutes == null)
        {
            KnownRoutes = new List<Route>();
        }

        if (route.DestinationRouterName == route.SourceRouterName)
        {
            // Ignorieren da Ziel und Quelle dieselben sind
        }
        else if (route.DestinationRouterName == RouterName)
        {
            Route new_route = new Route()
            {
                DestinationRouterName = route.SourceRouterName,
                SourceRouterName = RouterName,
                ViaRouter = route.ViaRouter,
                Cost = route.Cost,
            };
            Route knownRoute = KnownRoutes.Where(r => r.DestinationRouterName == new_route.DestinationRouterName).FirstOrDefault();

            if (knownRoute != null)
            {
                // Route zu dem Zielknoten ist bereits bekannt, prüfen ob die Kosten günstiger sind
                if (route.Cost < knownRoute.Cost)
                {
                    KnownRoutes.Remove(knownRoute);
                    KnownRoutes.Add(new_route);
                    LearnedNewRoutes = true;
                }
            }
            else
            {
                KnownRoutes.Add(new_route);
                LearnedNewRoutes = true;

            }
        }
        else if (route.SourceRouterName == RouterName)
        {
            Route knownRoute = KnownRoutes.Where(r => r.DestinationRouterName == route.DestinationRouterName).FirstOrDefault();

            if (knownRoute != null)
            {
                // Route zu dem Zielknoten ist bereits bekannt, prüfen ob die Kosten günstiger sind
                if (route.Cost < knownRoute.Cost)
                {
                    KnownRoutes.Remove(knownRoute);
                    KnownRoutes.Add(route);
                    LearnedNewRoutes = true;

                }
            }
            else
            {
                KnownRoutes.Add(route);
                LearnedNewRoutes = true;

            }
        }
    }


}

class Route
{
    public string SourceRouterName { get; set; }
    public string ViaRouter { get; set; }
    public string DestinationRouterName { get; set; }
    public int Cost { get; set; }
}