#!/usr/bin/perl

# (c) 2009, 2010 by Zeno Gantner <zeno.gantner@gmail.com>
#
# This file is part of MyMediaLite.
#
# MyMediaLite is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# MyMediaLite is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
#  You should have received a copy of the GNU General Public License
#  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.

use strict;
use warnings;

use English qw( -no_match_vars );
use File::Slurp;
use Getopt::Long;

my $USER_COLUMN  = 0;
my $ITEM_COLUMN  = 1;
my $EVENT_COLUMN = 2;

GetOptions(
       'help'                => \(my $help              = 0),
       'ignore-lines=i'      => \(my $ignore_lines      = 0),
       'number-of-columns=i' => \(my $number_of_columns = 0),
       'separator=s'         => \(my $separator         = '\\s+'),
       'user-column=i'       => \(my $user_column       = $USER_COLUMN),
       'item-column=i'       => \(my $item_column       = $ITEM_COLUMN),
       'event-column=i'      => \(my $event_column      = $EVENT_COLUMN),
       'date-column=i'       => \(my $date_column       = -1),
       'save-item-mapping=s' => \(my $save_item_mapping = ''),
       'save-user-mapping=s' => \(my $save_user_mapping = ''),
       'load-item-mapping=s' => \(my $load_item_mapping = ''),
       'load-user-mapping=s' => \(my $load_user_mapping = ''),
       'ignore-event=s'      => \(my $ignore_event      = undef),
       'event-constant=s'    => \(my $event_constant    = ''),
       'libsvm-format'       => \(my $libsvm_format     = 0),
) or usage(-1);

usage(0) if $help;

my $separator_regex    = qr{$separator};
my $ignore_event_regex = defined $ignore_event ? qr{$ignore_event} : qr{};

my $event_count = 0;
my %user_id     = $load_user_mapping ? read_hash($load_user_mapping) : ();
my %item_id     = $load_item_mapping ? read_hash($load_item_mapping) : ();
my $user_count  = scalar keys %user_id;
my $item_count  = scalar keys %item_id;

print STDERR "Start with $user_count and $item_count known users and items\n";

# skip lines
for (my $i = 0; $i < $ignore_lines; $i++) { <>; }

LINE:
while (<>) {
    my $line = $_;
    chomp $line;

    # ignore empty lines
    next LINE if $line eq '';

    my @fields = split $separator_regex, $line;
    die "Could not parse line: '$line'\n" if $number_of_columns && scalar @fields != $number_of_columns;

    my $user  = $fields[$user_column];
    my $item  = $fields[$item_column];
    my $event = $fields[$event_column];

    die "Undefined user (column $user_column) in line '$line'\n"   if not defined $user;
    die "Undefined item (column $item_column) in line '$line'\n"   if not defined $item;
    die "Undefined event (column $event_column) in line '$line'\n" if not defined $event;

    next LINE if defined $ignore_event && $event =~ m/$ignore_event_regex/;

    if (! exists $user_id{$user}) {
	$user_id{$user} = $user_count++;
    }
    if (! exists $item_id{$item}) {
	$item_id{$item} = $item_count++;
    }

    $event = $event_constant if $event_constant;

    if ($libsvm_format) {
	print "$event $user_id{$user}:1 $item_id{$item}:1\n";
    }
    else {
	print "$user_id{$user}\t$item_id{$item}\t$event";
	if ($date_column != -1) {
	    my $date = $fields[$date_column];
	    print "\t$date";
	}
	print "\n";
    }

    $event_count++;
}
print STDERR "Converted $event_count lines\n";
print STDERR "Now $user_count known users and $item_count known items\n";

if ($save_user_mapping) {
    write_hash($save_user_mapping, \%user_id);
    print STDERR "Wrote user IDs to file $save_user_mapping.\n";
}
if ($save_item_mapping) {
    write_hash($save_item_mapping, \%item_id);
    print STDERR "Wrote item IDs to file $save_item_mapping.\n";
}

sub write_hash {
    my ($filename, $hash_ref) = @_;

    open(my $FH, '>', $filename) or die $!;

    foreach my $key (sort keys %$hash_ref) {   # TODO make sorting configurable
	print $FH "$key\t$hash_ref->{$key}\n";
    }
}

# TODO move this into a library
sub read_hash {
    my ($filename) = @_;

    my @lines = read_file($filename);
    my %hash = map { chomp; split /\t/ } @lines;

    return %hash;
}

sub usage {
    my ($return_code) = @_;

    print << "END";
$PROGRAM_NAME

convert column-oriented dataset into a standard, MovieLens-like format

usage: $PROGRAM_NAME [OPTIONS] [INPUT]

    --help                    display this usage information
    --ignore-lines=N          ignore the first N lines
    --number-of-columns=N     check whether there are exactly N columns in each line
    --separator=REGEX         the separator regex used to split the lines into columns,
                              default is \\s+ (one or more whitespace characters)
    --user-column=N           specifies the user column (0-based), default is $USER_COLUMN
    --item-column=N           specifies the item column (0-based), default is $ITEM_COLUMN
    --event-column=N          specifies the event column (0-based), default is $EVENT_COLUMN
    --date-column=N           specifies the date column (0-based); if set, date will be appended to the output
    --save-user-mapping=FILE  write user ID mappings to FILE
    --save-item-mapping=FILE  write item ID mappings to FILE
    --load-user-mapping=FILE  use user ID mappings from FILE
    --load-item-mapping=FILE  use item ID mappings from FILE
    --ignore-event=REGEX      don't include events into the resulting dataset that match REGEX
    --event-constant=STRING   set the value for each event to STRING
    --libsvm-format           output in LIBSVM format (ignores date/timestamp information)
END
    exit $return_code;
}
