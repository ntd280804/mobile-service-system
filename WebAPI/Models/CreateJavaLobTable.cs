using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class CreateJavaLobTable
{
    public string? Name { get; set; }

    public byte[]? Lob { get; set; }

    public DateTime? Loadtime { get; set; }
}
