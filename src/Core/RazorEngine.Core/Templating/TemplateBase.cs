﻿namespace RazorEngine.Templating
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;

    using Text;

    /// <summary>
    /// Provides a base implementation of a template.
    /// </summary>
    public abstract class TemplateBase : MarshalByRefObject, ITemplate
    {
        #region Fields
        private ExecuteContext _context;
        #endregion

        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="TemplateBase"/>.
        /// </summary>
        protected TemplateBase() { }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the layout template name.
        /// </summary>
        public string Layout { get; set; }

        /// <summary>
        /// Gets or sets the template service.
        /// </summary>
        public ITemplateService TemplateService { get; set; }

        /// <summary>
        /// Gets the viewbag that allows sharing state between layout and child templates.
        /// </summary>
        public dynamic ViewBag { get { return _context.ViewBag; } }
        #endregion

        #region Methods
        /// <summary>
        /// Defines a section that can written out to a layout.
        /// </summary>
        /// <param name="name">The name of the section.</param>
        /// <param name="action">The delegate used to write the section.</param>
        public void DefineSection(string name, Action action)
        {
            _context.DefineSection(name, action);
        }

        /// <summary>
        /// Includes the template with the specified name.
        /// </summary>
        /// <param name="cacheName">The name of the template type in cache.</param>
        /// <param name="model">The model or NULL if there is no model for the template.</param>
        /// <returns>The template writer helper.</returns>
        public virtual TemplateWriter Include(string cacheName, object model)
        {
            var instance = TemplateService.Resolve(cacheName, model);
            if (instance == null)
                throw new ArgumentException("No template could be resolved with name '" + cacheName + "'");

            return new TemplateWriter(tw => tw.Write(instance.Run(new ExecuteContext())));
        }

        /// <summary>
        /// Determines if the section with the specified name has been defined.
        /// </summary>
        /// <param name="name">The section name.</param>
        /// <returns></returns>
        public virtual bool IsSectionDefined(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The name of the section to render must be specified.");

            return (_context.GetSectionDelegate(name) != null);
        }

        /// <summary>
        /// Executes the compiled template.
        /// </summary>
        public virtual void Execute() { }

        /// <summary>
        /// Returns the specified string as a raw string. This will ensure it is not encoded.
        /// </summary>
        /// <param name="rawString">The raw string to write.</param>
        /// <returns>An instance of <see cref="IEncodedString"/>.</returns>
        public IEncodedString Raw(string rawString)
        {
            return new RawString(rawString);
        }

        /// <summary>
        /// Resolves the layout template.
        /// </summary>
        /// <param name="name">The name of the layout template.</param>
        /// <returns>An instance of <see cref="ITemplate"/>.</returns>
        protected virtual ITemplate ResolveLayout(string name)
        {
            return TemplateService.Resolve(name, null);
        }

        /// <summary>
        /// Runs the template and returns the result.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>The merged result of the template.</returns>
        string ITemplate.Run(ExecuteContext context)
        {
            _context = context;

            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder)) 
            {
                _context.CurrentWriter = writer;
                Execute();
                _context.CurrentWriter = null;
            }

            if (Layout != null)
            {
                // Get the layout template.
                var layout = ResolveLayout(Layout);

                // Push the current body instance onto the stack for later execution.
                var body = new TemplateWriter(tw => tw.Write(builder.ToString()));
                context.PushBody(body);

                return layout.Run(context);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Renders the section with the specified name.
        /// </summary>
        /// <param name="name">The name of the section.</param>
        /// <param name="isRequired">Flag to specify whether the section is required.</param>
        /// <returns>The template writer helper.</returns>
        public TemplateWriter RenderSection(string name, bool isRequired = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The name of the section to render must be specified.");

            var action = _context.GetSectionDelegate(name);
            if (action == null && isRequired)
                throw new ArgumentException("No section has been defined with name '" + name + "'");

            if (action == null) action = () => { };

            return new TemplateWriter(tw => action());
        }

        /// <summary>
        /// Renders the body of the template.
        /// </summary>
        /// <returns>The template writer helper.</returns>
        public TemplateWriter RenderBody()
        {
            return _context.PopBody();
        }

        /// <summary>
        /// Writes the specified object to the result.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public virtual void Write(object value)
        {
            if (value == null) return;

            var encodedString = value as IEncodedString;
            if (encodedString != null)
            {
                _context.CurrentWriter.Write(encodedString);
            }
            else
            {
                encodedString = TemplateService.EncodedStringFactory.CreateEncodedString(value);
                _context.CurrentWriter.Write(encodedString);
            }
        }

        /// <summary>
        /// Writes the specified template helper result.
        /// </summary>
        /// <param name="helper">The template writer helper.</param>
        public virtual void Write(TemplateWriter helper)
        {
            if (helper == null)
                return;

            helper.WriteTo(_context.CurrentWriter);
        }

        /// <summary>
        /// Writes the specified string to the result.
        /// </summary>
        /// <param name="literal">The literal to write.</param>
        public virtual void WriteLiteral(string literal)
        {
            if (literal == null) return;
            _context.CurrentWriter.Write(literal);
        }

        /// <summary>
        /// Writes a string literal to the specified <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="literal">The literal to be written.</param>
        [Pure]
        public static void WriteLiteralTo(TextWriter writer, string literal)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (literal == null) return;
            writer.Write(literal);
        }

        /// <summary>
        /// Writes the specified object to the specified <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="value">The value to be written.</param>
        [Pure]
        public static void WriteTo(TextWriter writer, object value)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (value == null) return;
            writer.Write(value);
        }

        /// <summary>
        /// Writes the specfied template helper result to the specified writer.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="helper">The template writer helper.</param>
        [Pure]
        public static void WriteTo(TextWriter writer, TemplateWriter helper)
        {
            helper.WriteTo(writer);
        }
        #endregion
    }
}