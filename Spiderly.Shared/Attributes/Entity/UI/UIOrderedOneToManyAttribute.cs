namespace Spiderly.Shared.Attributes.Entity.UI
{
    /// <summary>
    /// <b>Usage:</b> Enables management of child entities through an ordered list in the parent entity's main UI form component. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class Course : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Name { get; set; }
    ///     
    ///     [UIOrderedOneToMany]
    ///     public virtual List&lt;Course&gt; Courses { get; set; } = new(); // Will be shown as an ordered list in the UI
    /// }
    /// 
    /// public class CourseItem : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Title { get; set; }
    ///     
    ///     [UIDoNotGenerate]
    ///     [Required]
    ///     public int OrderNumber { get; set; } // Needs to be called OrderNumber
    ///
    ///     [Required] // The course item can't exist without the course
    ///     [WithMany(nameof(Course.CourseItems))]
    ///     public virtual Course Course { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UIOrderedOneToManyAttribute : Attribute
    {
    }
}
