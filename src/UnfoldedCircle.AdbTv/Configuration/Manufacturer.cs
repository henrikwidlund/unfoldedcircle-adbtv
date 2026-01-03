using System.ComponentModel.DataAnnotations;

using NetEscapades.EnumGenerators;

using Theodicean.SourceGenerators;

namespace UnfoldedCircle.AdbTv.Configuration;

[EnumExtensions(IsInterceptable = true, MetadataSource = MetadataSource.DisplayAttribute)]
[EnumJsonConverter<Manufacturer>(CaseSensitive = false, PropertyName = "manufacturer")]
[JsonConverter(typeof(ManufacturerJsonConverter))]
public enum Manufacturer : sbyte
{
    GenericAndroid = 1,

    [Display(Name = "Fire TV")]
    FireTv,

    Hisense,

    Philips,

    [Display(Name = "TCL")]
    Tcl
}

// ReSharper disable once RedundantExtendsListEntry For some reason code won't compile without adding this explicit inheritance on this specific converter - all other work
public partial class ManufacturerJsonConverter : JsonConverter<Manufacturer>;