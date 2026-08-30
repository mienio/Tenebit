namespace Tenebit.Domain.Organizations;

/// <summary>
/// Which mark, if any, is printed above the QR code on an asset label.
///
/// <see cref="Custom"/> means the organization uploaded its own image, which is stored as bytes on the
/// organization rather than referenced by URL. The label is composed server-side and then rasterised in
/// the browser through a canvas, and both of those paths refuse to fetch remote images - an external URL
/// would silently print an empty box. Storing the bytes also keeps the server from making an outbound
/// request to an address the tenant supplied.
/// </summary>
public enum QrLabelLogoMode
{
    None = 0,
    Custom = 1,
    Tenebit = 2
}

/// <summary>
/// How much of the label the code itself claims, relative to the text around it.
///
/// The label is scaled to fit the stock it is printed on, so this is a ratio, not a measurement: a
/// larger code leaves less room for the caption and vice versa. It matters because every extra line of
/// text shrinks the code, and a code printed below roughly 0.4 mm per module stops scanning reliably
/// on a phone - a failure nobody notices until a sheet of labels is already on the boxes.
/// </summary>
public enum QrLabelCodeSize
{
    Small = 0,
    Medium = 1,
    Large = 2
}

/// <summary>The label stock the organization prints on. Kept with the organization because it is a
/// property of the paper they buy, not a choice to be re-made at every print.</summary>
public enum QrLabelFormat
{
    Square38 = 0,
    Medium63 = 1,
    Large99 = 2
}
