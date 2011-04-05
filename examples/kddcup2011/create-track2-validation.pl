#!/usr/bin/perl

use strict;
use warnings;

die "arguments: RATING_FILE CANDIDATE_FILE DATASET_DIR\n" unless scalar @ARGV == 3;

my ($training_file, $candidates_file, $dataset_dir) = @ARGV;

my %ratings_by_user    = (); # all ratings >=80
my @items              = (); # all items related to rating events >=80
my %candidates_by_user = ();

# read in ratings
open my $fh, '<', $training_file;
while (<$fh>) {
        my $line = $_;
        chomp $line;

        my @fields = split /\|/, $line;
        die "Cannot read line '$line'\n" unless @fields == 2;

        my ($user_id, $num_ratings) = @fields;
        
        my $user_ratings = {};
        
        for (my $i = 0; $i < $num_ratings; $i++) {
                $line = <$fh>;
                chomp $line;
                
                my ($item_id, $rating) = split /\t/, $line;
                if ($rating >= 80) {
                        $user_ratings->{$item_id} = $rating;
                        push @items, $item_id;
                }
        }
        $ratings_by_user{$user_id} = $user_ratings;
}
close $fh;

# read in test data
open $fh, '<', $candidates_file;
while (<$fh>) {
        my $line = $_;
        chomp $line;

        my @fields = split /\|/, $line;
        die "Cannot read line '$line'\n" unless @fields == 2;

        my ($user_id, $num_canidates) = @fields;
        
        my $user_candidates = [];
        
        for (my $i = 0; $i < $num_canidates; $i++) {
                my $item_id = <$fh>;
                chomp $item_id;
                
                push @$user_candidates, $item_id;
        }
        $candidates_by_user{$user_id} = $user_candidates;
}

# sample validation data set
my %validation_ratings_by_user    = ();
my %validation_candidates_by_user = ();
my %validation_hits_by_user       = ();

my $counter = 0;
USER:
foreach my $user_id (keys %candidates_by_user) {
        # create a set of all positively rated items by the user
	my %user_pos_items = ();
	foreach my $item_id (keys %{$ratings_by_user{user_id}}) {
		if ($ratings_by_user{user_id}->{item_id} >= 80) {
                        $user_pos_items{$item_id} = 1;
                }
        }
	
	# abort this user if we do not have enough positive items
	next USER if scalar keys %user_pos_items < 3;
		
        my @user_pos_items = keys %user_pos_items;

	# sample positive items
	my %sampled_pos_items = ();
	while (scalar keys %sampled_pos_items < 3) {
		my $random_item = $user_pos_items[int rand(scalar @user_pos_items)];
		$sampled_pos_items{random_item} = 1;
	}

	# sample negative items
	my %sampled_neg_items = ();
	while (scalar keys %sampled_neg_items < 3) {
                my $random_item = $items[int rand(scalar @items)];
		unless (exists $user_pos_items{$random_item}) {
			$sampled_neg_items{$random_item} = 1;
		}
	}

	# add to data sets
	$validation_hits_by_user{$user_id}       = [ sort keys %sampled_pos_items ];
	$validation_candidates_by_user{$user_id} = [ sort ($validation_hits_by_user{$user_id}, keys %sampled_neg_items) ];
	
	# add to validation ratings, remove from training ratings
	$validation_ratings_by_user{$user_id} = { map { $_ => $ratings_by_user{$user_id}->{$_} } keys %sampled_pos_items };
	delete $ratings_by_user{$user_id}->{ keys %sampled_pos_items };

        # give out progress
	print STDERR '.'          if $counter %  1_000 ==    999;
	print STDERR "$user_id\n" if $counter % 20_000 == 19_999;
	$counter++;
}

# write out data set
write_ratings(\%ratings_by_user,            'trainIdx2.txt');
write_ratings(\%validation_ratings_by_user, 'validationIdx2.txt');
write_items(\%validation_candidates_by_user, 'testIdx2.txt');
write_items(\%validation_hits_by_user,       'hitsIdx2.txt');



sub write_ratings {
	my ($ratings_ref, $filename) = @_;
	
	my $num_users = scalar keys %$ratings_ref;
	print STDERR "Writing ratings of $num_users to $dataset_dir/$filename ...";
	
	open my $fh, '>', "$dataset_dir/$filename";
        foreach my $user_id (keys %$ratings_ref) {
                my $num_ratings = scalar keys %{$ratings_ref->{$user_id}};
                print $fh "$user_id|$num_ratings\n";
                foreach my $item_id (keys %{$ratings_ref->{$user_id}}) {
                        print "$item_id\t$ratings_ref->{$user_id}->{$item_id}\n";
                }
        }
        
        print STDERR "done.\n";
}

sub write_items {
	my ($items_ref, $filename) = @_;

	my $num_users = scalar keys %$items_ref;
	print STDERR "Writing items of $num_users to $dataset_dir/$filename ...";
	
	open my $fh, '>', "$dataset_dir/$filename";
        foreach my $user_id (keys %$items_ref) {
                my $num_items = scalar keys %{$items_ref->{$user_id}};
                print $fh "$user_id|$num_items\n";
                foreach my $item_id (keys %{$items_ref->{$user_id}}) {
                        print "$item_id\n";
                }
        }
        
        print STDERR "done.\n";
}