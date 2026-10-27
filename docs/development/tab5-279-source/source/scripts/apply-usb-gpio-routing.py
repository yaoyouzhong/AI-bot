"""Keep ESP-IDF 5.4.2 P4 GPIO setup consistent with the USB PHY routing.

TAB5 uses PHY0 (GPIO24/25) for composite USB and GPIO26/27 for I2S.
The original GPIO LL assumes the default PHY mapping and disables USB_WRAP
when I2S claims GPIO26/27, even after USB_WRAP was routed to PHY0.
"""
import pathlib
import sys


def patch(text: str) -> str:
    marker = "// TAB5: honor software-swapped USB PHY routing during GPIO setup."
    if text.count(marker) == 2:
        return text
    replacements = []
    for condition in (
        "pin_name == IO_MUX_GPIO24_REG || pin_name == IO_MUX_GPIO25_REG",
        "gpio_num == USB_USJ_INT_PHY_DM_GPIO_NUM || gpio_num == USB_USJ_INT_PHY_DP_GPIO_NUM",
    ):
        second = (
            "pin_name == IO_MUX_GPIO26_REG || pin_name == IO_MUX_GPIO27_REG"
            if condition.startswith("pin_name") else
            "gpio_num == USB_OTG_INT_PHY_DM_GPIO_NUM || gpio_num == USB_OTG_INT_PHY_DP_GPIO_NUM"
        )
        old = f"""    if ({condition}) {{
        USB_SERIAL_JTAG.conf0.usb_pad_enable = 0;
    }} else if ({second}) {{
        USB_WRAP.otg_conf.usb_pad_enable = 0;
    }}"""
        new = f"""    {marker}
    const bool usb_swapped = LP_SYS.usb_ctrl.sw_hw_usb_phy_sel && LP_SYS.usb_ctrl.sw_usb_phy_sel;
    if ({condition}) {{
        if (usb_swapped) USB_WRAP.otg_conf.usb_pad_enable = 0;
        else USB_SERIAL_JTAG.conf0.usb_pad_enable = 0;
    }} else if ({second}) {{
        if (usb_swapped) USB_SERIAL_JTAG.conf0.usb_pad_enable = 0;
        else USB_WRAP.otg_conf.usb_pad_enable = 0;
    }}"""
        if text.count(old) != 1:
            raise RuntimeError("Unrecognized ESP32-P4 GPIO LL; review USB routing before building")
        replacements.append((old, new))
    for old, new in replacements:
        text = text.replace(old, new)
    if '#include "soc/lp_system_struct.h"' not in text:
        text = text.replace('#include "soc/usb_wrap_struct.h"', '#include "soc/usb_wrap_struct.h"\n#include "soc/lp_system_struct.h"')
    text = text.replace(
        "// We only consider the default connection here: PHY0 -> USJ, PHY1 -> USB_OTG",
        "// Respect both default and software-swapped PHY connections.")
    return text


if __name__ == "__main__":
    path = pathlib.Path(sys.argv[1]) / "components/hal/esp32p4/include/hal/gpio_ll.h"
    original = path.read_text(encoding="utf-8")
    result = patch(original)
    if result != original:
        path.write_text(result, encoding="utf-8")
    print("TAB5 USB GPIO routing patch ready")
