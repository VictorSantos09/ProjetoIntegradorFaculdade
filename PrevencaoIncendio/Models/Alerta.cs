﻿using Radzen;

namespace PrevencaoIncendio.Models;

public record Alerta(Variant Variant, AlertStyle Style, AlertSize Size, Shade Shade, string icon, string message);