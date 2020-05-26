set proj_name GBT_USB3_WYu
variable scriptfile [file normalize [info script]]
variable scriptdir [file dirname $scriptfile]
set firmware_dir $scriptdir/../../
set sources $scriptdir/../src
set project_dir $firmware_dir/projects/$proj_name

close_project -quiet
set PART xc7k410tffg900-2L
create_project -force -part $PART $proj_name $project_dir

set_property target_language verilog [current_project]
set_property default_lib work [current_project]

read_verilog -library work $sources/ez_usb_fx3_top.v
read_verilog -library work $sources/ez_usb_fx3_if.v
read_verilog -library work $sources/clk_gen.v
read_xdc -verbose $sources/ez_usb_fx3.xdc

