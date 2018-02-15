using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BrokenEvent.Shared.Algorithms
{
#if SHARED_PUBLIC_API
  public
#else
  internal
#endif
  class CommandLineParser<TModel>
    where TModel: class
  {
    public const string ARG_PREFIX1 = "-";
    public const string ARG_PREFIX2 = "/";

    private readonly string[] HELP_COMMANDS = new string[]
    {
      ARG_PREFIX1 + "?",
      ARG_PREFIX2 + "?",
      "help"
    };

    private readonly Dictionary<Type, Func<Type, string, object>> parsers = new Dictionary<Type, Func<Type, string, object>>
    {
      { typeof(bool), (t, value) => bool.Parse(value) },
      { typeof(int), (t, value) => int.Parse(value) },
      { typeof(double), (t, value) => double.Parse(value) },
      { typeof(byte), (t, value) => byte.Parse(value) },
      { typeof(string), (t, value) => value },
      { typeof(float), (t, value) => float.Parse(value) },
      { typeof(uint), (t, value) => uint.Parse(value) },
    };

    private List<PropertyDescriptor> descs = new List<PropertyDescriptor>();
    private List<PropertyDescriptor> unnamedDescs = new List<PropertyDescriptor>();
    private List<PropertyDescriptor> allDescs = new List<PropertyDescriptor>();
    private CommandModelAttribute modelAttribute;
    private TModel model;
    private StringComparer stringComparer = StringComparer.InvariantCultureIgnoreCase;
    private bool suppressUnknownArgs;
    private bool assignmentSyntax;
    private bool writeUsageOnError;
    private string error;

    private class PropertyDescriptor
    {
      public readonly CommandAttribute Attribute;
      private readonly PropertyInfo Property;
      private bool isSet;
      private readonly TModel model;
      private readonly Func<Type, string, object> valueParser;

      public PropertyDescriptor(CommandAttribute attribute, PropertyInfo property, CommandLineParser<TModel> parser)
      {
        Attribute = attribute;
        Property = property;
        model = parser.model;

        if (!attribute.IsFlag)
        {
          if (Property.PropertyType.IsEnum)
            valueParser = Enum.Parse;
          else if (!parser.parsers.TryGetValue(Property.PropertyType, out valueParser))
            throw new InvalidOperationException("Not supported property type: " + Property.PropertyType.Name);
        }
      }

      public void SetFlagValue(bool value)
      {
        Property.SetValue(model, value);
      }

      public string SetValue(string value)
      {
        try
        {
          Property.SetValue(model, valueParser(Property.PropertyType, value));
          isSet = true;
        }
        catch (Exception e)
        {
          return e.Message;
        }

        return null;
      }

      public bool IsRequiredSet
      {
        get { return isSet || !Attribute.IsRequired; }
      }
    }

    /// <summary>
    /// Creates the commandline parser instance and analyzes the model type.
    /// </summary>
    /// <param name="model">Model object to fill it with values from commandline args.</param>
    /// <exception cref="ArgumentException">Thrown if the model has flag property with non-boolean type or the model has a property without setter.</exception>
    /// <exception cref="InvalidOperationException">Thrown if model has a property with unsupported type.</exception>
    public CommandLineParser(TModel model)
    {
      this.model = model;

      Type modelType = typeof(TModel);
      modelAttribute = modelType.GetCustomAttribute<CommandModelAttribute>();
      Dictionary<int, PropertyDescriptor> unnamedDict = new Dictionary<int, PropertyDescriptor>();

      PropertyInfo[] infos = modelType.GetProperties();
      foreach (PropertyInfo info in infos)
      {
        CommandAttribute attr = info.GetCustomAttribute<CommandAttribute>();
        if (attr == null)
          continue;

        if (attr.IsFlag && info.PropertyType != typeof(bool))
          throw new ArgumentException($"{info.Name} is marked as flag but has type {info.PropertyType.Name}. Only boolean property can be flag.");

        if (!info.CanWrite)
          throw new ArgumentException($"{info.Name} has no setter.");

        PropertyDescriptor desc = new PropertyDescriptor(attr, info, this);
        if (attr.Name == null)
          unnamedDict.Add(desc.Attribute.UnnamedIndex, desc);
        else
          descs.Add(desc);

        allDescs.Add(desc);
      }

      int lastRequiredIndex = -1;
      for (int i = 0; i < unnamedDict.Count; i++)
      {
        PropertyDescriptor desc;
        if (!unnamedDict.TryGetValue(i, out desc))
          throw new ArgumentException($"Missing unnamed index {i}");

        if (desc.Attribute.IsRequired)
        {
          if (i > lastRequiredIndex + 1)
            throw new ArgumentException($"Can't set unnamed arg #{i} as required as unnamed args before are not required.");

          lastRequiredIndex = i;
        }

        unnamedDescs.Add(desc);
      }
    }

    private void OnError(string error)
    {
      this.error = error;
      Console.WriteLine(error);
      if (writeUsageOnError)
        WriteUsage();
    }

    /// <summary>
    /// Parse the command line and fill model with values.
    /// </summary>
    /// <param name="args">Commandline args from environment.</param>
    /// <returns>True if parsing successfull and application should continue. False to exit immediately (on errors or help request).</returns>
    public bool Parse(string[] args)
    {
      // welcome
      if (modelAttribute != null)
      {
        Console.WriteLine(modelAttribute.WelcomeText);
        Console.WriteLine();
      }

      // help?
      if (args.Length > 0)
        for (int i = 0; i < HELP_COMMANDS.Length; i++)
          if (stringComparer.Equals(args[0], HELP_COMMANDS[i]))
          {
            WriteUsage();
            return false;
          }
      
      PropertyDescriptor desc = null;
      int unnamedIndex = 0;

      for (int i = 0; i < args.Length; i++)
      {
        string arg = args[i];
        if (arg.StartsWith(ARG_PREFIX1) || arg.StartsWith(ARG_PREFIX2))
        {
          if (desc != null)
          {
            OnError(string.Format("Missing value for argument: {0}", desc.Attribute.Name));
            return false;
          }

          if (assignmentSyntax)
          {
            int index = arg.IndexOf('=');
            if (index != -1)
            {
              desc = FindDesc(arg.Substring(1, index - 1));
              if (desc.Attribute.IsFlag)
              {
                OnError(string.Format("Unable to assign flag: {0}", desc.Attribute.Name));
                return false;
              }

              desc.SetValue(arg.Substring(index + 1));
              desc = null;
              continue;
            }
          }

          desc = FindDesc(arg.Substring(1));
          if (desc == null)
          {
            if (suppressUnknownArgs)
              continue;

            OnError(string.Format("Invalid argument: {0}", arg));
            return false;
          }

          if (desc.Attribute.IsFlag)
          {
            desc.SetFlagValue(true);
            desc = null;
          }
        }
        else
        {
          if (desc == null)
          {
            if (unnamedIndex >= unnamedDescs.Count)
            {
              if (suppressUnknownArgs)
                continue;

              OnError(string.Format("Invalid unnamed argument: {0}", arg));
              return false;
            }

            unnamedDescs[unnamedIndex++].SetValue(arg);
          }
          else
          {
            string error = desc.SetValue(arg);
            if (error != null)
            {
              OnError(string.Format("Failed to parse {0}: {1}", desc.Attribute.Name, error));
              return false;
            }
            desc = null;
          }
        }
      }

      if (desc != null)
      {
        OnError(string.Format("Missing value for argument: {0}", desc.Attribute.Name));
        return false;
      }

      // validate
      for (int i = 0; i < allDescs.Count; i++)
      {
        PropertyDescriptor d = allDescs[i];

        if (!d.IsRequiredSet)
        {
          if (d.Attribute.Name != null)
            OnError(string.Format("Required argument is missing: {0}", d.Attribute.Name));
          else
            OnError(string.Format("Required argument is missing: #{0}", d.Attribute.UnnamedIndex));
          return false;
        }
      }

      return true;
    }

    private PropertyDescriptor FindDesc(string value)
    {
      for (int i = 0; i < descs.Count; i++)
      {
        PropertyDescriptor desc = descs[i];
        if (stringComparer.Equals(desc.Attribute.Name, value))
          return desc;

        if (!string.IsNullOrEmpty(desc.Attribute.Alias) && stringComparer.Equals(desc.Attribute.Alias, value))
          return desc;
      }

      return null;
    }

    private void WriteUsage()
    {
      if (modelAttribute != null)
      {
        Console.WriteLine(modelAttribute.UsageText);
        Console.WriteLine();
      }

      Console.WriteLine("Usage:");
      Assembly assembly = Assembly.GetEntryAssembly();
      if (assembly == null)
        Console.Write("app.exe");
      else
        Console.Write(Path.GetFileName(assembly.Location));

      foreach (PropertyDescriptor desc in allDescs)
      {
        if (desc.Attribute.Name == null)
        {
          if (desc.Attribute.IsRequired)
            Console.Write(" {0}", desc.Attribute.UsageExample);
          else
            Console.Write(" [{0}]", desc.Attribute.UsageExample);
        }
        else
        {
          if (desc.Attribute.IsFlag)
            Console.Write(" [{0}{1}]", ARG_PREFIX1, desc.Attribute.Name);
          else if (desc.Attribute.IsRequired)
            Console.Write(" {0}{1} {2}", ARG_PREFIX1, desc.Attribute.Name, desc.Attribute.UsageExample);
          else
            Console.Write(" [{0}{1} {2}]", ARG_PREFIX1, desc.Attribute.Name, desc.Attribute.UsageExample);
        }
      }
      Console.WriteLine();
      Console.WriteLine();

      Console.WriteLine("Arguments:");

      foreach (PropertyDescriptor desc in allDescs)
      {
        if (string.IsNullOrEmpty(desc.Attribute.Usage))
          continue;

        string name;
        if (desc.Attribute.Name == null)
          name = desc.Attribute.UsageExample;
        else
        {
          name = ARG_PREFIX1 + desc.Attribute.Name;
          if (!string.IsNullOrEmpty(desc.Attribute.Alias))
            name += ", " + ARG_PREFIX1 + desc.Attribute.Alias;
        }

        string comment = desc.Attribute.Usage;
        if (!desc.Attribute.IsRequired)
          comment += " Optional.";

        Console.WriteLine("{0,-16} {1}", name, comment);
      }
    }

    /// <summary>
    /// Gets the parser's model.
    /// </summary>
    public TModel Model
    {
      get { return model; }
    }

    /// <summary>
    /// Gets or sets the value indicating whether the parser should be case-sensitive (used for arg names).
    /// </summary>
    public bool CaseSensitive
    {
      get { return stringComparer != StringComparer.InvariantCultureIgnoreCase; }
      set
      {
        if (value)
          stringComparer = StringComparer.InvariantCulture;
        else
          stringComparer = StringComparer.InvariantCultureIgnoreCase;
      }
    }

    /// <summary>
    /// Gets or sets the value indicating whether the parser should ignore unknown arguments. Will fail if disabled.
    /// </summary>
    public bool SuppressUnknownArgs
    {
      get { return suppressUnknownArgs; }
      set { suppressUnknownArgs = value; }
    }

    /// <summary>
    /// Gets or sets the value indicating whether the assignment syntax is enabled. Assignment syntax allows to set args as
    /// <code>/arg=value</code>
    /// along with usual
    /// <code>/arg value</code>
    /// </summary>
    /// <remarks>If this syntax is enabled, the values cannot have '=' symbol.</remarks>
    public bool AssignmentSyntax
    {
      get { return assignmentSyntax; }
      set { assignmentSyntax = value; }
    }

    /// <summary>
    /// Gets or sets the value indicating whether usages should written on error.
    /// </summary>
    public bool WriteUsageOnError
    {
      get { return writeUsageOnError; }
      set { writeUsageOnError = value; }
    }

    /// <summary>
    /// Gets the error if <see cref="Parse"/> returned false.
    /// </summary>
    public string Error
    {
      get { return error; }
    }
  }

  [AttributeUsage(AttributeTargets.Property)]
#if SHARED_PUBLIC_API
  public
#else
  internal
# endif
  class CommandAttribute: Attribute
  {
    public int UnnamedIndex { get; }
    public string Usage { get; }
    public string UsageExample { get; }
    public string Name { get; }
    public string Alias { get; }
    public bool IsRequired { get; }
    public bool IsFlag { get; }

    /// <summary>
    /// Creates the attribute instance for named command.
    /// </summary>
    /// <param name="usage">Usage description for the parameter.</param>
    /// <param name="name">Parameter name to use in commandline.</param>
    /// <param name="alias">Parameter alias (alternative name) to use in commandline.</param>
    /// <param name="usageExample">Value example in usage.</param>
    /// <param name="isRequired">True if the param is required and false otherwise.</param>
    /// <param name="isFlag">Is flag, no value required. Valid only for boolean properties.</param>
    public CommandAttribute(string name, string usage, string usageExample = "value", string alias = null, bool isRequired = false, bool isFlag = false)
    {
      if (string.IsNullOrEmpty(name))
        throw new ArgumentNullException(nameof(name));
      if (isRequired && isFlag)
        throw new ArgumentException("Invalid settings. Property can't be flag and required at same time.");

      Usage = usage;
      Name = name;
      Alias = alias;
      IsRequired = isRequired;
      IsFlag = isFlag;
      UsageExample = usageExample;
      UnnamedIndex = -1;
    }

    /// <summary>
    /// Creates the attribute instance for unnamed command.
    /// </summary>
    /// <param name="unnamedIndex">Zero-based index of unnamed argument.</param>
    /// <param name="usage">Usage description for the parameter.</param>
    /// <param name="alias">Parameter alias (alternative name) to use in commandline.</param>
    /// <param name="usageExample">Value example in usage.</param>
    /// <param name="isRequired">True if the param is required and false otherwise.</param>
    public CommandAttribute(int unnamedIndex, string usage, string usageExample = "value", string alias = null, bool isRequired = false)
    {
      UnnamedIndex = unnamedIndex;
      Usage = usage;
      UsageExample = usageExample;
      Alias = alias;
      IsRequired = isRequired;
    }
  }

  [AttributeUsage(AttributeTargets.Class)]
#if SHARED_PUBLIC_API
  public
#else
  internal
#endif
  class CommandModelAttribute: Attribute
  {
    public string WelcomeText { get; }
    public string UsageText { get; }

    /// <summary>
    /// Creates the attribute instance.
    /// </summary>
    /// <param name="welcomeText">Welcome text for commandline interface: copyright, etc.</param>
    /// <param name="usageText">Additional usage text.</param>
    public CommandModelAttribute(string welcomeText, string usageText = null)
    {
      WelcomeText = welcomeText;
      UsageText = usageText;
    }
  }
}
