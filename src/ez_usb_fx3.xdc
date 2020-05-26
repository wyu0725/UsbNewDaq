####configure SPI Flash
set_property BITSTREAM.CONFIG.CONFIGRATE 50 [current_design]
set_property CFGBVS VCCO [current_design]
set_property CONFIG_VOLTAGE 3.3 [current_design]
set_property BITSTREAM.CONFIG.SPI_BUSWIDTH 4 [current_design]

### usb interface
set_property -dict {PACKAGE_PIN AD4 IOSTANDARD LVCMOS18} [get_ports pclk]
set_property -dict {PACKAGE_PIN AG7 IOSTANDARD LVCMOS18} [get_ports {addr[0]}]
set_property -dict {PACKAGE_PIN AC6 IOSTANDARD LVCMOS18} [get_ports {addr[1]}]
set_property -dict {PACKAGE_PIN AC1 IOSTANDARD LVCMOS18} [get_ports {dio[0]}]
set_property -dict {PACKAGE_PIN AD2 IOSTANDARD LVCMOS18} [get_ports {dio[1]}]
set_property -dict {PACKAGE_PIN AG3 IOSTANDARD LVCMOS18} [get_ports {dio[2]}]
set_property -dict {PACKAGE_PIN AD1 IOSTANDARD LVCMOS18} [get_ports {dio[3]}]
set_property -dict {PACKAGE_PIN AF1 IOSTANDARD LVCMOS18} [get_ports {dio[4]}]
set_property -dict {PACKAGE_PIN AC2 IOSTANDARD LVCMOS18} [get_ports {dio[5]}]
set_property -dict {PACKAGE_PIN AE1 IOSTANDARD LVCMOS18} [get_ports {dio[6]}]
set_property -dict {PACKAGE_PIN AE4 IOSTANDARD LVCMOS18} [get_ports {dio[7]}]
set_property -dict {PACKAGE_PIN AH1 IOSTANDARD LVCMOS18} [get_ports {dio[8]}]
set_property -dict {PACKAGE_PIN AC5 IOSTANDARD LVCMOS18} [get_ports {dio[9]}]
set_property -dict {PACKAGE_PIN AG2 IOSTANDARD LVCMOS18} [get_ports {dio[10]}]
set_property -dict {PACKAGE_PIN AJ1 IOSTANDARD LVCMOS18} [get_ports {dio[11]}]
set_property -dict {PACKAGE_PIN AK1 IOSTANDARD LVCMOS18} [get_ports {dio[12]}]
set_property -dict {PACKAGE_PIN AK3 IOSTANDARD LVCMOS18} [get_ports {dio[13]}]
set_property -dict {PACKAGE_PIN AD6 IOSTANDARD LVCMOS18} [get_ports {dio[14]}]
set_property -dict {PACKAGE_PIN AF2 IOSTANDARD LVCMOS18} [get_ports {dio[15]}]
set_property -dict {PACKAGE_PIN AJ6 IOSTANDARD LVCMOS18} [get_ports {dio[16]}]
set_property -dict {PACKAGE_PIN AD3 IOSTANDARD LVCMOS18} [get_ports {dio[17]}]
set_property -dict {PACKAGE_PIN AK6 IOSTANDARD LVCMOS18} [get_ports {dio[18]}]
set_property -dict {PACKAGE_PIN AJ7 IOSTANDARD LVCMOS18} [get_ports {dio[19]}]
set_property -dict {PACKAGE_PIN AJ3 IOSTANDARD LVCMOS18} [get_ports {dio[20]}]
set_property -dict {PACKAGE_PIN AJ8 IOSTANDARD LVCMOS18} [get_ports {dio[21]}]
set_property -dict {PACKAGE_PIN AK8 IOSTANDARD LVCMOS18} [get_ports {dio[22]}]
set_property -dict {PACKAGE_PIN AH5 IOSTANDARD LVCMOS18} [get_ports {dio[23]}]
set_property -dict {PACKAGE_PIN AH7 IOSTANDARD LVCMOS18} [get_ports {dio[24]}]
set_property -dict {PACKAGE_PIN AJ9 IOSTANDARD LVCMOS18} [get_ports {dio[25]}]
set_property -dict {PACKAGE_PIN AG5 IOSTANDARD LVCMOS18} [get_ports {dio[26]}]
set_property -dict {PACKAGE_PIN AH6 IOSTANDARD LVCMOS18} [get_ports {dio[27]}]
set_property -dict {PACKAGE_PIN AJ2 IOSTANDARD LVCMOS18} [get_ports {dio[28]}]
set_property -dict {PACKAGE_PIN AK10 IOSTANDARD LVCMOS18} [get_ports {dio[29]}]
set_property -dict {PACKAGE_PIN AH9 IOSTANDARD LVCMOS18} [get_ports {dio[30]}]
set_property -dict {PACKAGE_PIN AG9 IOSTANDARD LVCMOS18} [get_ports {dio[31]}]

set_property -dict {PACKAGE_PIN AG4 IOSTANDARD LVCMOS18} [get_ports flaga]
set_property -dict {PACKAGE_PIN AH2 IOSTANDARD LVCMOS18} [get_ports flagb]
set_property -dict {PACKAGE_PIN AK4 IOSTANDARD LVCMOS18} [get_ports flagc]
set_property -dict {PACKAGE_PIN AH4 IOSTANDARD LVCMOS18} [get_ports flagd]

set_property -dict {PACKAGE_PIN AC4 IOSTANDARD LVCMOS18} [get_ports slcs_n]
set_property -dict {PACKAGE_PIN AF3 IOSTANDARD LVCMOS18} [get_ports slrd_n]
set_property -dict {PACKAGE_PIN AE3 IOSTANDARD LVCMOS18} [get_ports sloe_n]
set_property -dict {PACKAGE_PIN AJ4 IOSTANDARD LVCMOS18} [get_ports slwr_n]
set_property -dict {PACKAGE_PIN AE5 IOSTANDARD LVCMOS18} [get_ports pktend_n]

set_property -dict {PACKAGE_PIN AG22 IOSTANDARD LVCMOS33} [get_ports usb_init]
set_property -dict {PACKAGE_PIN AF20 IOSTANDARD LVCMOS33} [get_ports usb_reset]

set_property -dict {PACKAGE_PIN N25 IOSTANDARD LVCMOS33} [get_ports usb_init_led]
set_property -dict {PACKAGE_PIN N26 IOSTANDARD LVCMOS33} [get_ports usb_reset_led]


### clk and rst
set_property -dict {PACKAGE_PIN AF22 IOSTANDARD LVCMOS33} [get_ports clk]
set_property -dict {PACKAGE_PIN A15 IOSTANDARD LVCMOS33} [get_ports rst_n]

### timing constraints


create_clock -period 25.000 -name clk -waveform {0.000 12.500} [get_ports clk]
