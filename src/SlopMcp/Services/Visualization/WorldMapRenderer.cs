using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using ScottPlot;
using SlopMcp.Models.Visualization;

namespace SlopMcp.Services.Visualization {

  public class WorldMapRenderer
  {
    private readonly IReadOnlyList<WorldCountry> _countries;
    private readonly Dictionary<string, WorldCountry> _byCode;
    private readonly Dictionary<string, WorldCountry> _byName;

    public const int DefaultWidth = 1800;
    public const int DefaultHeight = 860;
    private const string ResourceName = "SlopMcp.Resources.ne_110m_admin_0_countries.geojson";
    private const string AntarcticaIso3 = "ATA";
    private const string MissingCode = "-99";

    private static readonly Color NoDataColor = Color.FromHex("#D9D9D9");
    private static readonly Color BorderColor = Color.FromHex("#FFFFFF");
    private static readonly Color OceanColor = Color.FromHex("#F2F6FA");
    private static readonly string[] NameProperties = ["NAME", "NAME_LONG", "ADMIN", "NAME_EN", "FORMAL_EN", "NAME_SORT", "NAME_RU"];

    private static readonly IReadOnlyDictionary<string, IColormap> Colormaps = new Dictionary<string, IColormap>(StringComparer.OrdinalIgnoreCase)
    {
      ["viridis"] = new ScottPlot.Colormaps.Viridis(),
      ["plasma"] = new ScottPlot.Colormaps.Plasma(),
      ["inferno"] = new ScottPlot.Colormaps.Inferno(),
      ["magma"] = new ScottPlot.Colormaps.Magma(),
      ["turbo"] = new ScottPlot.Colormaps.Turbo(),
      ["blues"] = new ScottPlot.Colormaps.Blues(),
      ["greens"] = new ScottPlot.Colormaps.Greens(),
      ["reds"] = new ScottPlot.Colormaps.Amp(),
      ["balance"] = new ScottPlot.Colormaps.Balance()
    };

    private static readonly IReadOnlyDictionary<string, string> NameAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["UK"] = "GB",
      ["Britain"] = "GB",
      ["Great Britain"] = "GB",
      ["England"] = "GB",
      ["UAE"] = "AE",
      ["America"] = "US",
      ["United States of America"] = "US",
      ["Russia"] = "RU",
      ["РФ"] = "RU",
      ["South Korea"] = "KR",
      ["Korea"] = "KR",
      ["North Korea"] = "KP",
      ["Czechia"] = "CZ",
      ["Czech Republic"] = "CZ",
      ["Ivory Coast"] = "CI",
      ["DRC"] = "CD",
      ["Congo-Kinshasa"] = "CD",
      ["Vietnam"] = "VN",
      ["Iran"] = "IR",
      ["Syria"] = "SY",
      ["Laos"] = "LA",
      ["Bolivia"] = "BO",
      ["Venezuela"] = "VE",
      ["Tanzania"] = "TZ",
      ["Taiwan"] = "TW",
      ["Kosovo"] = "XK",
      ["Palestine"] = "PS"
    };

    public WorldMapRenderer()
    {
      _countries = LoadCountries();
      _byCode = new Dictionary<string, WorldCountry>(StringComparer.OrdinalIgnoreCase);
      _byName = new Dictionary<string, WorldCountry>(StringComparer.OrdinalIgnoreCase);
      // Official ISO codes first so another country's ADM0_A3 cannot shadow them.
      foreach(WorldCountry country in _countries)
      {
        string?[] isoCodes = [country.Iso2, country.Iso3];
        foreach(string? code in isoCodes)
        {
          if(code is not null)
          {
            _byCode.TryAdd(code, country);
          }
        }
      }

      foreach(WorldCountry country in _countries)
      {
        foreach(string code in country.Codes)
        {
          _byCode.TryAdd(code, country);
        }

        foreach(string name in country.Names)
        {
          _byName.TryAdd(NormalizeName(name), country);
        }
      }

      foreach((string alias, string iso2) in NameAliases)
      {
        if(_byCode.TryGetValue(iso2, out WorldCountry? country))
        {
          _byName.TryAdd(NormalizeName(alias), country);
        }
      }
    }

    public bool TryResolveCountry(string input, [NotNullWhen(true)] out WorldCountry? country)
    {
      country = null;
      if(string.IsNullOrWhiteSpace(input))
      {
        return false;
      }

      string key = input.Trim();
      if(key.Length <= 3 && _byCode.TryGetValue(key, out WorldCountry? byCode))
      {
        country = byCode;
        return true;
      }

      if(_byName.TryGetValue(NormalizeName(key), out WorldCountry? byName))
      {
        country = byName;
        return true;
      }

      return false;
    }

    public WorldMapResult Render(
      IReadOnlyList<CountryValue> values,
      string? title = null,
      string? legendLabel = null,
      string? colormap = null,
      int width = DefaultWidth,
      int height = DefaultHeight
    )
    {
      if(values is null || values.Count == 0)
      {
        throw new ArgumentException("At least one country value is required.", nameof(values));
      }

      IColormap map = ResolveColormap(colormap);
      Dictionary<WorldCountry, CountryValue> resolved = new(ReferenceEqualityComparer.Instance);
      List<string> unresolved = [];
      foreach(CountryValue value in values)
      {
        if(value.Value is double v && (double.IsNaN(v) || double.IsInfinity(v)))
        {
          throw new ArgumentException($"Value for '{value.Country}' is NaN or infinite.", nameof(values));
        }

        if(value.Color is not null && !TryParseHex(value.Color, out _))
        {
          throw new ArgumentException($"Invalid color '{value.Color}' for '{value.Country}'. Use hex like #E41A1C.", nameof(values));
        }

        if(TryResolveCountry(value.Country, out WorldCountry? country))
        {
          resolved[country] = value;
        }
        else
        {
          unresolved.Add(value.Country);
        }
      }

      if(resolved.Count == 0)
      {
        throw new ArgumentException($"None of the countries could be resolved: {string.Join(", ", unresolved)}. Use ISO codes (US, USA) or English names.", nameof(values));
      }

      double[] numbers = [.. resolved.Values.Where(v => v.Value.HasValue && v.Color is null).Select(v => v.Value!.Value)];
      double min = numbers.Length > 0 ? numbers.Min() : 0;
      double max = numbers.Length > 0 ? numbers.Max() : 0;
      if(numbers.Length > 0 && max <= min)
      {
        double halfSpan = min == 0 ? 0.5 : Math.Abs(min) * 0.05;
        min -= halfSpan;
        max += halfSpan;
      }

      bool includeAntarctica = resolved.Keys.Any(c => c.Iso3 == AntarcticaIso3);
      var plot = new Plot();
      plot.DataBackground.Color = OceanColor;

      foreach(WorldCountry country in _countries)
      {
        if(country.Iso3 == AntarcticaIso3 && !includeAntarctica)
        {
          continue;
        }

        Color fill = NoDataColor;
        if(resolved.TryGetValue(country, out CountryValue? value))
        {
          if(value.Color is not null && TryParseHex(value.Color, out Color explicitColor))
          {
            fill = explicitColor;
          }
          else if(value.Value is double number)
          {
            double t = max > min ? (number - min) / (max - min) : 0.5;
            fill = map.GetColor(t);
          }
        }

        foreach(Coordinates[] ring in country.Polygons)
        {
          ScottPlot.Plottables.Polygon polygon = plot.Add.Polygon(ring);
          polygon.FillColor = fill;
          polygon.LineColor = BorderColor;
          polygon.LineWidth = 0.6f;
        }
      }

      if(numbers.Length > 0)
      {
        ScottPlot.Panels.ColorBar colorBar = plot.Add.ColorBar(new ColorAxisSource(map, min, max), Edge.Right);
        if(!string.IsNullOrWhiteSpace(legendLabel))
        {
          colorBar.Label = legendLabel;
        }
      }

      plot.HideAxesAndGrid();
      if(!string.IsNullOrWhiteSpace(title))
      {
        plot.Title(title);
      }

      plot.Axes.SetLimits(-180, 180, includeAntarctica ? -90 : -58, 84);
      plot.Axes.SquareUnits();
      plot.Font.Automatic();

      return new WorldMapResult
      {
        Png = plot.GetImageBytes(width, height, ImageFormat.Png),
        UnresolvedCountries = unresolved
      };
    }

    private static IColormap ResolveColormap(string? name)
    {
      if(string.IsNullOrWhiteSpace(name))
      {
        return Colormaps["viridis"];
      }

      if(Colormaps.TryGetValue(name.Trim(), out IColormap? colormap))
      {
        return colormap;
      }

      throw new ArgumentException($"Unknown colormap '{name}'. Supported: {string.Join(", ", Colormaps.Keys)}.", nameof(name));
    }

    private static bool TryParseHex(string hex, out Color color)
    {
      color = default;
      string trimmed = hex.Trim().TrimStart('#');
      if(trimmed.Length is not (6 or 8) || !trimmed.All(Uri.IsHexDigit))
      {
        return false;
      }

      color = Color.FromHex(trimmed);
      return true;
    }

    private static string NormalizeName(string name) => string.Join(' ', name.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));

    private static List<WorldCountry> LoadCountries()
    {
      using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
      using JsonDocument document = JsonDocument.Parse(stream);

      List<WorldCountry> countries = [];
      foreach(JsonElement feature in document.RootElement.GetProperty("features").EnumerateArray())
      {
        JsonElement properties = feature.GetProperty("properties");
        List<Coordinates[]> polygons = ReadPolygons(feature.GetProperty("geometry"));
        if(polygons.Count == 0)
        {
          continue;
        }

        string? iso2 = GetCode(properties, "ISO_A2_EH");
        string? iso3 = GetCode(properties, "ISO_A3_EH");
        string? adm0 = GetCode(properties, "ADM0_A3");
        string?[] codes = [iso2, iso3, adm0];
        countries.Add(new WorldCountry
        {
          Name = GetString(properties, "NAME") ?? GetString(properties, "ADMIN") ?? "Unknown",
          Iso2 = iso2,
          Iso3 = iso3 ?? adm0,
          Codes = [.. codes.OfType<string>()],
          Names = [.. NameProperties.Select(p => GetString(properties, p)).Where(n => !string.IsNullOrWhiteSpace(n)).OfType<string>()],
          Polygons = polygons,
          Area = polygons.Sum(RingArea)
        });
      }

      // Larger countries first so enclaves (e.g. Lesotho inside South Africa) are drawn on top.
      return [.. countries.OrderByDescending(c => c.Area)];
    }

    private static List<Coordinates[]> ReadPolygons(JsonElement geometry)
    {
      List<Coordinates[]> rings = [];
      string? type = geometry.GetProperty("type").GetString();
      JsonElement coordinates = geometry.GetProperty("coordinates");
      switch(type)
      {
        case "Polygon":
          rings.Add(ReadRing(coordinates[0]));
          break;
        case "MultiPolygon":
          foreach(JsonElement polygon in coordinates.EnumerateArray())
          {
            rings.Add(ReadRing(polygon[0]));
          }
          break;
      }

      return rings;
    }

    private static Coordinates[] ReadRing(JsonElement ring) =>
      [.. ring.EnumerateArray().Select(point => new Coordinates(point[0].GetDouble(), point[1].GetDouble()))];

    private static double RingArea(Coordinates[] ring)
    {
      double sum = 0;
      for(int i = 0; i < ring.Length; i++)
      {
        Coordinates a = ring[i];
        Coordinates b = ring[(i + 1) % ring.Length];
        sum += a.X * b.Y - b.X * a.Y;
      }

      return Math.Abs(sum) / 2;
    }

    private static string? GetString(JsonElement properties, string name) =>
      properties.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? GetCode(JsonElement properties, string name)
    {
      string? code = GetString(properties, name);
      return string.IsNullOrWhiteSpace(code) || code == MissingCode ? null : code;
    }
  }

}
