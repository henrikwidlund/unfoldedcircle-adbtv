using System.ComponentModel.DataAnnotations;

using NetEscapades.EnumGenerators;

using UnfoldedCircle.Models;

namespace UnfoldedCircle.AdbTv.Configuration;

[EnumExtensions(IsInterceptable = true, MetadataSource = MetadataSource.DisplayAttribute)]
[JsonConverter(typeof(StrictEnumConverter<Manufacturer>))]
public enum Manufacturer : byte
{
    [Display(Name = Constants.GenericAndroid)]
    [JsonStringEnumMemberName(Constants.GenericAndroid)]
    Android = 1,

    [Display(Name = Constants.FireTv)]
    [JsonStringEnumMemberName(Constants.FireTv)]
    FireTv,

    Hisense,
    Panasonic,
    Philips,

    [Display(Name = Constants.PhilipsAlternate)]
    [JsonStringEnumMemberName(Constants.PhilipsAlternate)]
    PhilipsAlternate,

    Sony,

    [Display(Name = Constants.Tcl)]
    [JsonStringEnumMemberName(Constants.Tcl)]
    Tcl
}

file static class Constants
{
    public const string GenericAndroid = "Generic Android";
    public const string FireTv = "Fire TV";
    public const string PhilipsAlternate = "Philips (Alternate)";
    public const string Tcl = "TCL";
}
